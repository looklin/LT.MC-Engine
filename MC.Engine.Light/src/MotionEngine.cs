/// <summary>
/// 轻量级运动控制引擎
/// 
/// 功能说明：
/// 实现类 PLC 的启动/暂停/继续/停止功能，适用于非标自动化设备
/// 
/// 设计特点：
/// 1. 极简主义：仅依赖 .NET 原生类型，无第三方库
/// 2. 线程安全：所有公共 API 均保证线程安全
/// 3. 事件驱动：通过事件通知外部状态变化
/// 4. 任务协调：支持多任务并行执行和任务间通信
/// 
/// 使用示例：
/// var engine = new MotionEngine();
/// await engine.StartAsync(async ctx => {
///     await RunTaskA(ctx);
///     await RunTaskB(ctx);
/// });
/// engine.Pause();
/// engine.Resume();
/// engine.Stop();
/// </summary>

using System;
using System.Threading;
using System.Threading.Tasks;

namespace MC.Engine.Light
{
    /// <summary>
    /// 运动控制引擎核心类
    /// </summary>
    public class MotionEngine : IDisposable
    {
        /// <summary>状态机实例：管理引擎状态转换</summary>
        private readonly StateMachine _stateMachine;

        /// <summary>当前执行上下文：负责暂停/继续/停止的线程协调</summary>
        private ExecutionContext? _context;

        /// <summary>线程同步锁：防止并发调用公共 API</summary>
        private readonly object _lock = new object();

        /// <summary>
        /// 当前引擎状态（只读）
        /// </summary>
        public EngineState State => _stateMachine.CurrentState;

        /// <summary>状态变更事件</summary>
        public event EventHandler<StateChangedEventArgs>? StateChanged;

        /// <summary>错误发生事件</summary>
        public event EventHandler<ErrorEventArgs>? ErrorOccurred;

        /// <summary>步骤变更事件（由任务代码触发）</summary>
        public event EventHandler<StepChangedEventArgs>? StepChanged;

        /// <summary>
        /// 初始化引擎
        /// </summary>
        public MotionEngine()
        {
            _stateMachine = new StateMachine();

            // 转发状态机的事件到外部
            _stateMachine.StateChanged += (s, e) => StateChanged?.Invoke(this, e);
        }

        /// <summary>
        /// 启动引擎
        /// 
        /// 执行流程：
        /// 1. 验证当前状态是否为 Idle 或 Error（其他状态不允许启动）
        /// 2. 状态转换：Idle → Running
        /// 3. 创建执行上下文（打开闸门）
        /// 4. 执行用户提供的任务函数
        /// 5. 任务完成后状态转换：Running → Idle
        /// 
        /// 异常处理：
        /// - OperationCanceledException：正常停止，不视为错误
        /// - 其他异常：进入 Error 状态，需要手动 Reset
        /// </summary>
        /// <param name="taskRunner">
        /// 任务执行函数。接收 ExecutionContext 参数，用于检查暂停/停止信号
        /// </param>
        /// <param name="cancellationToken">外部取消令牌（可选）</param>
        public async Task StartAsync(Func<ExecutionContext, Task> taskRunner, CancellationToken cancellationToken = default)
        {
            // 第一步：状态验证（线程安全）
            lock (_lock)
            {
                if (State != EngineState.Idle && State != EngineState.Error)
                    throw new InvalidStateException(State, EngineCommand.Start);
            }

            // 第二步：状态转换
            if (!_stateMachine.TryTransition(EngineCommand.Start))
                throw new InvalidOperationException("无法启动引擎：状态转换失败");

            // 第三步：创建执行上下文（闸门自动打开）
            _context = new ExecutionContext();

            try
            {
                // 第四步：执行用户任务
                // 注意：这里使用 ConfigureAwait(false) 防止在 WinForms 环境下死锁
                await taskRunner(_context).ConfigureAwait(false);

                // 第五步：任务正常完成，转换回 Idle
                _stateMachine.TryTransition(EngineCommand.StopComplete);
            }
            catch (OperationCanceledException) when (_context.CancellationToken.IsCancellationRequested)
            {
                // 用户调用了 Stop()，属于正常停止，不视为错误
                _stateMachine.TryTransition(EngineCommand.StopComplete);
            }
            catch (Exception ex)
            {
                // 其他异常：记录错误并进入 Error 状态
                OnError(ex.Message, ErrorSeverity.Fatal);
                _stateMachine.ForceState(EngineState.Error);
                throw; // 重新抛出异常，让调用方知道出错了
            }
            finally
            {
                // 清理资源
                _context?.Dispose();
                _context = null;
            }
        }

        /// <summary>
        /// 暂停引擎
        /// 
        /// 工作原理：关闭执行上下文的闸门，所有任务将在下次 WaitIfPaused() 处阻塞
        /// 调用时机：必须在 Running 状态下调用
        /// </summary>
        public void Pause()
        {
            lock (_lock)
            {
                if (State != EngineState.Running)
                    throw new InvalidStateException(State, EngineCommand.Pause);
            }

            _context?.RequestPause();
            _stateMachine.TryTransition(EngineCommand.Pause);
        }

        /// <summary>
        /// 继续引擎
        /// 
        /// 工作原理：打开执行上下文的闸门，释放所有阻塞的任务线程
        /// 调用时机：必须在 Paused 状态下调用
        /// </summary>
        public void Resume()
        {
            lock (_lock)
            {
                if (State != EngineState.Paused)
                    throw new InvalidStateException(State, EngineCommand.Resume);
            }

            _context?.RequestResume();
            _stateMachine.TryTransition(EngineCommand.Resume);
        }

        /// <summary>
        /// 停止引擎
        /// 
        /// 工作原理：
        /// 1. 先打开闸门（释放暂停的任务）
        /// 2. 发送取消信号（使 WaitIfPaused 抛出异常）
        /// 3. 等待任务循环退出
        /// 
        /// 调用时机：Running 或 Paused 状态下均可
        /// </summary>
        /// <param name="mode">停止模式（预留扩展，当前未使用）</param>
        public void Stop(StopMode mode = StopMode.Smooth)
        {
            lock (_lock)
            {
                if (State != EngineState.Running && State != EngineState.Paused)
                    throw new InvalidStateException(State, EngineCommand.Stop);
            }

            _context?.RequestStop();
            _stateMachine.TryTransition(EngineCommand.Stop);
        }

        /// <summary>
        /// 复位引擎（从错误状态恢复）
        /// 
        /// 工作流程：将状态从 Error 转换回 Idle
        /// 调用时机：必须在 Error 状态下调用
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                if (State != EngineState.Error)
                    throw new InvalidStateException(State, EngineCommand.Reset);
            }

            _stateMachine.TryTransition(EngineCommand.Reset);
        }

        /// <summary>
        /// 触发步骤变更事件（公开方法，供外部调用）
        /// </summary>
        public void ReportStepChanged(string taskName, string previousStep, string currentStep)
        {
            StepChanged?.Invoke(this, new StepChangedEventArgs(taskName, previousStep, currentStep));
        }

        /// <summary>
        /// 触发错误事件
        /// </summary>
        protected void OnError(string message, ErrorSeverity severity)
        {
            ErrorOccurred?.Invoke(this, new ErrorEventArgs(message, severity));
        }

        /// <summary>
        /// 触发步骤变更事件（内部使用）
        /// </summary>
        protected void OnStepChanged(string taskName, string previousStep, string currentStep)
        {
            StepChanged?.Invoke(this, new StepChangedEventArgs(taskName, previousStep, currentStep));
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
