/// <summary>
/// 执行上下文：使用 ManualResetEventSlim 作为全局闸门控制任务运行/暂停
/// 设计参考：PLC 的 RUN/STOP 模式
/// 
/// 工作原理：
/// 1. 启动时闸门打开（Set），任务正常执行
/// 2. 暂停时闸门关闭（Reset），所有任务在 WaitIfPaused() 处阻塞
/// 3. 继续时闸门打开（Set），释放所有阻塞的任务
/// 4. 停止时先打开闸门，再发送取消信号，确保任务能正常退出
/// </summary>

using System;
using System.Threading;
using System.Threading.Tasks;

namespace MC.Engine.Light
{
    /// <summary>
    /// 执行上下文：负责暂停/继续/停止的线程协调
    /// 每个任务循环中应在关键步骤前调用 WaitIfPaused()
    /// </summary>
    public class ExecutionContext : IDisposable
    {
        /// <summary>全局运行闸门：Set()=打开(运行)，Reset()=关闭(暂停)</summary>
        private readonly ManualResetEventSlim _runGate;

        /// <summary>停止标志：标记是否正在执行停止流程</summary>
        private volatile bool _isStopping;

        /// <summary>取消令牌源：用于通知任务取消</summary>
        private readonly CancellationTokenSource _cts;

        /// <summary>任务间通信标志：用于 TaskA 等待 TaskB 完成某操作</summary>
        private volatile bool _awaitCondition;

        /// <summary>
        /// 初始化执行上下文
        /// 闸门初始状态为打开，允许任务立即开始执行
        /// </summary>
        public ExecutionContext()
        {
            _runGate = new ManualResetEventSlim(initialState: true);
            _cts = new CancellationTokenSource();
        }

        /// <summary>获取取消令牌，用于传递给底层异步操作</summary>
        public CancellationToken CancellationToken => _cts.Token;

        /// <summary>
        /// 暂停检查点：在每个运动步骤执行前调用此方法
        /// 
        /// 行为说明：
        /// - 如果正在停止：立即抛出 OperationCanceledException，中断任务循环
        /// - 如果已暂停：阻塞在此处，直到继续信号到达
        /// - 正常运行：立即返回，继续执行后续步骤
        /// </summary>
        public void WaitIfPaused()
        {
            // 停止优先检查：如果正在停止，直接抛出异常退出循环
            if (_isStopping)
            {
                throw new OperationCanceledException("停止请求已发出", _cts.Token);
            }

            // 暂停检查：闸门关闭时阻塞等待
            _runGate.Wait(_cts.Token);
        }

        /// <summary>
        /// 等待条件满足（用于任务间同步）
        /// 典型场景：TaskA 在 step1 等待 TaskB 将准备条件设为 true
        /// 
        /// 实现逻辑：
        /// 1. 循环检查条件函数返回值
        /// 2. 每次检查前调用 WaitIfPaused，确保暂停时也能正确阻塞
        /// 3. 支持超时机制，防止无限等待
        /// </summary>
        /// <param name="condition">条件函数，返回 true 表示条件满足</param>
        /// <param name="timeoutMs">超时时间（毫秒），-1 或 0 表示永不超时</param>
        /// <exception cref="TimeoutException">等待超时时抛出</exception>
        public async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 10000)
        {
            int elapsed = 0;
            while (!condition())
            {
                WaitIfPaused(); // 暂停时也要阻塞，防止暂停期间条件检查继续消耗CPU
                await Task.Delay(10, _cts.Token).ConfigureAwait(false);

                if (timeoutMs > 0)
                {
                    elapsed += 10;
                    if (elapsed >= timeoutMs)
                        throw new TimeoutException("等待条件超时");
                }
            }
        }

        /// <summary>
        /// 请求暂停：关闭闸门
        /// 调用后，所有任务将在下次 WaitIfPaused() 处阻塞
        /// </summary>
        public void RequestPause()
        {
            _runGate.Reset();
        }

        /// <summary>
        /// 请求继续：打开闸门
        /// 调用后，所有阻塞在 WaitIfPaused() 的任务将继续执行
        /// </summary>
        public void RequestResume()
        {
            _runGate.Set();
        }

        /// <summary>
        /// 请求停止：释放闸门 + 发送取消信号
        /// 
        /// 重要：必须先 Set() 再 Cancel()，原因：
        /// 1. Set() 释放闸门，让暂停的任务可以继续走到 WaitIfPaused()
        /// 2. Cancel() 使 WaitIfPaused() 抛出异常，退出任务循环
        /// 如果顺序颠倒，暂停的任务会永远阻塞在 WaitIfPaused()
        /// </summary>
        public void RequestStop()
        {
            _isStopping = true;
            _runGate.Set();   // 第一步：释放闸门
            _cts.Cancel();    // 第二步：发送取消信号
        }

        /// <summary>设置条件标志（用于任务间通信）</summary>
        public void SetCondition(bool value) => _awaitCondition = value;

        /// <summary>获取条件标志</summary>
        public bool GetCondition() => _awaitCondition;

        /// <summary>释放所有托管资源</summary>
        public void Dispose()
        {
            _runGate.Dispose();
            _cts.Dispose();
        }
    }
}
