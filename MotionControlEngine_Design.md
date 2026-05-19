# C# 运动控制流程引擎技术方案

> 类 PLC 启动/暂停/继续/停止功能设计

---

## 一、系统概述

本方案设计一套基于 C# 的运动控制流程引擎（Motion Control Engine），实现类似 PLC 的任务生命周期管理，包括：

| 操作 | 说明 |
|------|------|
| **启动 (Start)** | 初始化引擎，开始执行运动任务序列 |
| **暂停 (Pause)** | 安全挂起当前执行的任务，保持状态 |
| **继续 (Resume)** | 从暂停点恢复任务执行 |
| **停止 (Stop)** | 终止任务，可选择是否回到安全位置 |

---

## 二、架构设计

### 2.1 整体架构

```
┌─────────────────────────────────────────────────────────────┐
│                      应用层 (Application)                     │
│          Start() / Pause() / Resume() / Stop()               │
└─────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────┐
│                    控制引擎层 (Engine Core)                   │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌────────────┐  │
│  │ 状态机   │  │ 命令队列 │  │ 任务调度 │  │ 异常处理   │  │
│  │ StateMgr │  │ CmdQueue │  │ Scheduler│  │  Handler   │  │
│  └──────────┘  └──────────┘  └──────────┘  └────────────┘  │
└─────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────┐
│                    执行层 (Executor Layer)                    │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌────────────┐  │
│  │ 轴控制器 │  │ 插补器   │  │ 位置反馈 │  │ 安全监控   │  │
│  │ AxisCtrl │  │ Interp   │  │  Feedback│  │  Safety    │  │
│  └──────────┘  └──────────┘  └──────────┘  └────────────┘  │
└─────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────┐
│                    硬件抽象层 (HAL - Hardware Abstraction)    │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌────────────┐  │
│  │ 接口定义 │  │ 卡厂商A  │  │ 卡厂商B  │  │  仿真器    │  │
│  │ IHalCard │  │ Adapter  │  │ Adapter  │  │  Adapter   │  │
│  └──────────┘  └──────────┘  └──────────┘  └────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 分层职责

| 层级 | 职责 | 关键技术 |
|------|------|----------|
| **应用层** | 对外暴露 API，接收控制指令 | 公开接口、事件回调 |
| **控制引擎层** | 状态管理、命令解析、任务编排 | 状态机、生产者-消费者模型 |
| **执行层** | 具体运动指令的生成与执行 | 插补算法、轨迹规划 |
| **硬件抽象层** | 屏蔽不同运动控制卡差异 | 接口抽象、适配器模式 |

---

## 三、核心状态机设计

### 3.1 状态定义

```
                    ┌──────────────────────────────────────┐
                    │                                      ▼
  ┌─────┐  Start   ┌────────┐  Pause   ┌────────┐  Resume  ┌─────────┐
  │ Idle├─────────►│Running ├─────────►│Paused  ├─────────►│ Running │
  └─────┘          └───┬────┘          └────┬───┘          └────┬────┘
       ▲               │                    │                   │
       │    Stop       │     Stop           │      Stop         │
       │               ▼                    ▼                   ▼
       │          ┌─────────────────────────────────────────────┐
       │          │                 Stopping                     │
       │          └────────────────────┬────────────────────────┘
       │                               │
       │         停止完成/急停完成      │
       └───────────────────────────────┘
```

### 3.2 状态枚举

```csharp
public enum EngineState
{
    /// <summary>空闲状态，等待启动</summary>
    Idle = 0,
    
    /// <summary>正在运行</summary>
    Running = 1,
    
    /// <summary>已暂停</summary>
    Paused = 2,
    
    /// <summary>正在停止（减速停止过程）</summary>
    Stopping = 3,
    
    /// <summary>错误状态，需要复位</summary>
    Error = 4
}
```

### 3.3 状态转换矩阵

```csharp
/// <summary>
/// 状态转换规则：CurrentState + Command => NextState
/// </summary>
private static readonly Dictionary<(EngineState, EngineCommand), EngineState> TransitionRules = 
    new Dictionary<(EngineState, EngineCommand), EngineState>
    {
        // 空闲状态
        { (EngineState.Idle, EngineCommand.Start), EngineState.Running },
        
        // 运行状态
        { (EngineState.Running, EngineCommand.Pause), EngineState.Stopping },   // 先减速停止再进入Paused
        { (EngineState.Running, EngineCommand.Stop), EngineState.Stopping },
        
        // 暂停状态
        { (EngineState.Paused, EngineCommand.Resume), EngineState.Running },
        { (EngineState.Paused, EngineCommand.Stop), EngineState.Stopping },
        
        // 停止中
        { (EngineState.Stopping, EngineCommand.StopComplete), EngineState.Idle },
        
        // 任意状态 → 错误
        { (EngineState.Running, EngineCommand.EmergencyStop), EngineState.Error },
        { (EngineState.Paused, EngineCommand.EmergencyStop), EngineState.Error },
    };
```

---

## 四、核心接口设计

### 4.1 引擎主接口

```csharp
/// <summary>
/// 运动控制引擎核心接口
/// </summary>
public interface IMotionEngine : IDisposable
{
    // === 生命周期控制 ===
    Task StartAsync(MotionTask task, CancellationToken ct = default);
    Task PauseAsync(CancellationToken ct = default);
    Task ResumeAsync(CancellationToken ct = default);
    Task StopAsync(StopMode mode = StopMode.Smooth, CancellationToken ct = default);
    Task ResetAsync(CancellationToken ct = default);
    
    // === 状态查询 ===
    EngineState State { get; }
    MotionTask? CurrentTask { get; }
    double CurrentProgress { get; }  // 0.0 ~ 1.0
    
    // === 事件 ===
    event EventHandler<StateChangedEventArgs>? StateChanged;
    event EventHandler<TaskProgressEventArgs>? ProgressChanged;
    event EventHandler<ErrorEventArgs>? ErrorOccurred;
}
```

### 4.2 运动任务定义

```csharp
/// <summary>
/// 运动任务：包含一系列运动指令
/// </summary>
public class MotionTask
{
    public string TaskId { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    
    /// <summary>运动指令序列</summary>
    public IList<MotionCommand> Commands { get; set; } = new List<MotionCommand>();
    
    /// <summary>任务级配置</summary>
    public TaskConfiguration Config { get; set; } = new();
}

/// <summary>
/// 单条运动指令
/// </summary>
public abstract class MotionCommand
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public CommandPriority Priority { get; set; }
    
    /// <summary>执行前的准备动作（如减速到目标速度）</summary>
    public abstract Task PrepareAsync(IExecutionContext context);
    
    /// <summary>核心执行逻辑</summary>
    public abstract Task ExecuteAsync(IExecutionContext context);
    
    /// <summary>执行后的清理（如回零）</summary>
    public abstract Task CleanupAsync(IExecutionContext context);
}

/// <summary>
/// 常用运动指令实现
/// </summary>
public class MoveAbsoluteCommand : MotionCommand { /* ... */ }
public class MoveRelativeCommand : MotionCommand { /* ... */ }
public class HomeCommand : MotionCommand { /* ... */ }
public class DwellCommand : MotionCommand { /* 等待/延时 */ }
public class WaitInputCommand : MotionCommand { /* 等待输入信号 */ }
```

### 4.3 停止模式

```csharp
public enum StopMode
{
    /// <summary>平滑停止：减速到零</summary>
    Smooth = 0,
    
    /// <summary>快速停止：按最大减速度停止</summary>
    Quick = 1,
    
    /// <summary>急停：立即切断动力（硬件级）</summary>
    Emergency = 2
}
```

---

## 五、核心实现流程

### 5.1 启动流程

```
StartAsync()
    │
    ├── 1. 验证当前状态（必须为 Idle）
    │   └── 否则抛出 InvalidOperationException
    │
    ├── 2. 初始化状态步骤（如 stepA = StepA.stepA0）
    │
    ├── 3. 状态转换：Idle → Running
    │
    ├── 4. 初始化执行上下文
    │   ├── 创建 CancellationTokenSource
    │   ├── 打开 ManualResetEvent 闸门（_runGate.Set()）
    │   ├── 初始化共享变量（awaitCondition 等）
    │   └── 加载硬件适配器
    │
    ├── 5. 启动多任务执行循环
    │   │
    │   └── Task.Run(() => RunTaskA/B(cts.Token))
    │       │
    │       └── while (state == Running && !ct.IsCancellationRequested)
    │           │
    │           ├── _runGate.WaitOne()  ← 暂停闸门，所有任务在此阻塞
    │           │
    │           ├── 检查 awaitCondition → 如需要则等待条件满足
    │           │
    │           ├── 根据当前 Step 执行对应逻辑
    │           │   └── switch (currentStep) { case StepX.step0: ... }
    │           │
    │           └── 更新步骤状态（触发日志记录）
    │
    ├── 6. 触发完成事件
    │
    └── 7. 状态转换：Running → Idle
```

### 5.2 暂停流程

```
PauseAsync()
    │
    ├── 1. 验证当前状态（必须为 Running）
    │
    ├── 2. 关闭全局闸门（_runGate.Reset()）
    │   └── 所有任务在下次 WaitOne() 时阻塞
    │
    ├── 3. 状态转换：Running → Paused
    │
    ├── 4. 触发暂停完成事件
    │
    └── 5. 日志记录：状态冻结
```

### 5.3 继续流程

```
ResumeAsync()
    │
    ├── 1. 验证当前状态（必须为 Paused）
    │
    ├── 2. 打开全局闸门（_runGate.Set()）
    │   └── 释放所有阻塞的任务线程
    │
    ├── 3. 状态转换：Paused → Running
    │
    └── 4. 触发恢复事件
```

### 5.4 停止流程

```
StopAsync(mode)
    │
    ├── 1. 验证当前状态（Running 或 Paused）
    │
    ├── 2. 状态转换：→ Stopping
    │
    ├── 3. 根据停止模式执行停止策略
    │   │
    │   ├── Smooth: 按配置减速度减速
    │   ├── Quick:  按最大允许减速度减速
    │   └── Emergency:
    │       ├── 立即调用 HAL.EmergencyStop()
    │       ├── 切断所有轴动力
    │       └── 触发急停事件
    │
    ├── 4. 等待停止完成（轴速度 == 0）
    │
    ├── 5. 状态转换：Stopping → Idle
    │
    └── 6. 触发停止完成事件
```

---

## 六、关键实现技术

### 6.1 基于 ManualResetEvent 的全局闸门机制

```csharp
/// <summary>
/// 执行上下文：使用 ManualResetEvent 作为全局闸门，控制所有任务的运行/暂停
/// 参考 PLC 的 RUN/STOP 模式，设计简洁高效
/// </summary>
internal class ExecutionContext : IExecutionContext
{
    /// <summary>全局运行闸门：Set()=运行，Reset()=暂停</summary>
    private readonly ManualResetEventSlim _runGate = new(true);
    
    /// <summary>任务间通信：等待条件标志</summary>
    private volatile bool _awaitCondition;
    
    private volatile bool _isStopping;
    private readonly CancellationTokenSource _cts = new();
    
    public CancellationToken CancellationToken => _cts.Token;
    
    /// <summary>
    /// 暂停检查点：每个步骤执行前调用
    /// 如果暂停，则所有任务阻塞在此处
    /// </summary>
    public void WaitIfPaused()
    {
        // 如果正在停止，抛出取消异常
        if (_isStopping)
        {
            throw new OperationCanceledException("停止请求已发出");
        }
        
        // 如果闸门关闭（暂停），则阻塞等待
        _runGate.Wait();
    }
    
    /// <summary>
    /// 等待条件满足（用于任务间同步）
    /// 例如：TaskA 在 step1 等待 TaskB 将 _awaitCondition 设为 true
    /// </summary>
    public async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 10000)
    {
        while (!condition())
        {
            WaitIfPaused();  // 暂停时也要阻塞
            await Task.Delay(10, CancellationToken);
            
            if (timeoutMs <= 0)
                throw new TimeoutException("等待条件超时");
            timeoutMs -= 10;
        }
    }
    
    public void RequestPause() => _runGate.Reset();   // 关闭闸门
    
    public void RequestResume() => _runGate.Set();    // 打开闸门
    
    public void RequestStop()
    {
        _isStopping = true;
        _runGate.Set();   // 释放闸门，让任务可以退出循环
        _cts.Cancel();    // 发送取消信号
    }
    
    public void SetCondition(bool value) => _awaitCondition = value;
    public bool GetCondition() => _awaitCondition;
}
```

### 6.1.1 任务间通信示例

```csharp
/// <summary>
/// TaskA 执行流程
/// </summary>
private async Task RunTaskA(IExecutionContext ctx)
{
    var step = StepA.StepA0;
    
    while (step != StepA.None && !ctx.CancellationToken.IsCancellationRequested)
    {
        ctx.WaitIfPaused();  // 暂停检查点
        
        switch (step)
        {
            case StepA.StepA0:
                // 初始化
                await MoveAxisAsync(0, 0, ctx);
                step = StepA.StepA1;
                break;
                
            case StepA.StepA1:
                // 等待 TaskB 完成某操作
                await ctx.WaitUntilAsync(() => ctx.GetCondition());
                step = StepA.StepA2;
                break;
                
            case StepA.StepA2:
                // 执行运动
                await MoveAxisAsync(0, 100, ctx);
                step = StepA.StepA3;
                break;
                
            // ...更多步骤
        }
        
        // 每个步骤完成后记录日志
        Log($"TaskA: {step}");
    }
}

/// <summary>
/// TaskB 执行流程
/// </summary>
private async Task RunTaskB(IExecutionContext ctx)
{
    var step = StepB.StepB0;
    
    while (step != StepB.None && !ctx.CancellationToken.IsCancellationRequested)
    {
        ctx.WaitIfPaused();  // 暂停检查点
        
        switch (step)
        {
            case StepB.StepB0:
                // 初始化
                step = StepB.StepB1;
                break;
                
            case StepB.StepB1:
                // 设置条件，通知 TaskA 可以继续
                ctx.SetCondition(true);
                step = StepB.StepB2;
                break;
                
            // ...更多步骤
        }
        
        Log($"TaskB: {step}");
    }
}
```

### 6.2 基于 Channel 的命令队列

```csharp
/// <summary>
/// 使用 System.Threading.Channels 实现高性能命令队列
/// </summary>
public class CommandQueue
{
    private readonly Channel<MotionCommand> _channel = 
        Channel.CreateBounded<MotionCommand>(
            new BoundedChannelOptions(1000)
            {
                SingleWriter = true,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            });
    
    public ValueTask EnqueueAsync(MotionCommand cmd, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(cmd, ct);
    
    public async Task<MotionCommand> DequeueAsync(CancellationToken ct = default) =>
        await _channel.Reader.ReadAsync(ct);
}
```

### 6.3 状态机管理器

```csharp
/// <summary>
/// 线程安全的状态机管理器
/// </summary>
public class StateMachine
{
    private EngineState _currentState = EngineState.Idle;
    private readonly object _lock = new();
    
    public EngineState CurrentState
    {
        get => _currentState;
        private set => _currentState = value;
    }
    
    /// <summary>
    /// 尝试执行状态转换
    /// </summary>
    /// <returns>是否转换成功</returns>
    public bool TryTransition(EngineCommand command)
    {
        lock (_lock)
        {
            var key = (CurrentState, command);
            if (TransitionRules.TryGetValue(key, out var nextState))
            {
                var previous = CurrentState;
                CurrentState = nextState;
                StateChanged?.Invoke(this, new StateChangedEventArgs(previous, nextState));
                return true;
            }
            return false;
        }
    }
    
    public event EventHandler<StateChangedEventArgs>? StateChanged;
}
```

### 6.4 硬件抽象层接口

```csharp
/// <summary>
/// 硬件抽象层：运动控制卡接口
/// </summary>
public interface IHalMotionCard : IDisposable
{
    // === 轴控制 ===
    Task MoveAxisAsync(int axisIndex, double targetPosition, double velocity, CancellationToken ct);
    Task StopAxisAsync(int axisIndex, StopMode mode, CancellationToken ct);
    Task EmergencyStopAllAsync(CancellationToken ct);
    
    // === 状态读取 ===
    Task<AxisStatus> GetAxisStatusAsync(int axisIndex, CancellationToken ct);
    Task<double[]> GetCurrentPositionAsync(CancellationToken ct);
    
    // === I/O ===
    Task<bool> ReadInputAsync(int inputIndex, CancellationToken ct);
    Task WriteOutputAsync(int outputIndex, bool value, CancellationToken ct);
    
    // === 事件 ===
    event EventHandler<HardwareErrorEventArgs>? HardwareError;
}

/// <summary>
/// 轴状态信息
/// </summary>
public record AxisStatus
{
    public bool IsEnabled { get; init; }
    public bool IsMoving { get; init; }
    public double CurrentVelocity { get; init; }
    public double CurrentPosition { get; init; }
    public bool IsInError { get; init; }
}
```

---

## 七、扩展性设计

### 7.1 插件化命令系统

```csharp
/// <summary>
/// 命令注册表：支持运行时注册新命令类型
/// </summary>
public class CommandRegistry
{
    private readonly Dictionary<string, Func<MotionCommand>> _factories = new();
    
    public void Register<T>(string commandType) where T : MotionCommand, new()
    {
        _factories[commandType] = () => new T();
    }
    
    public MotionCommand Create(string commandType)
    {
        if (_factories.TryGetValue(commandType, out var factory))
        {
            return factory();
        }
        throw new KeyNotFoundException($"未知命令类型: {commandType}");
    }
}
```

### 7.2 中间件管道（Middleware Pipeline）

```csharp
/// <summary>
/// 命令执行中间件：类似 ASP.NET Core 的中间件管道
/// </summary>
public interface IMiddleware
{
    Task InvokeAsync(IExecutionContext context, Func<Task> next);
}

/// <summary>
/// 示例中间件：
/// 1. 日志记录中间件
/// 2. 安全检查中间件
/// 3. 性能监控中间件
/// 4. 数据记录中间件
/// </summary>
public class LoggingMiddleware : IMiddleware
{
    private readonly ILogger _logger;
    
    public async Task InvokeAsync(IExecutionContext context, Func<Task> next)
    {
        _logger.LogInformation("开始执行命令: {CommandId}", context.CurrentCommand?.Id);
        await next();
        _logger.LogInformation("命令执行完成: {CommandId}", context.CurrentCommand?.Id);
    }
}
```

### 7.3 事件驱动架构

```csharp
/// <summary>
/// 引擎事件参数
/// </summary>
public class StateChangedEventArgs : EventArgs
{
    public EngineState PreviousState { get; }
    public EngineState NewState { get; }
    public DateTime Timestamp { get; }
    
    public StateChangedEventArgs(EngineState previous, EngineState @new)
    {
        PreviousState = previous;
        NewState = @new;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// 典型事件使用方式
/// </summary>
public class ExampleUsage
{
    public async Task Example(IMotionEngine engine)
    {
        engine.StateChanged += (sender, e) =>
        {
            Console.WriteLine($"状态变更: {e.PreviousState} → {e.NewState}");
        };
        
        engine.ErrorOccurred += (sender, e) =>
        {
            Console.WriteLine($"错误: {e.ErrorMessage}");
            // 自动记录到日志系统
        };
        
        await engine.StartAsync(myTask);
    }
}
```

---

## 八、项目结构

```
MC-Engine/
│
├── src/
│   ├── MC.Engine/                    # 核心引擎
│   │   ├── MotionEngine.cs           # 主引擎实现
│   │   ├── StateMachine.cs           # 状态机
│   │   ├── ExecutionContext.cs       # 执行上下文（含 ManualResetEvent 闸门）
│   │   └── TaskScheduler.cs          # 多任务调度器
│   │
│   ├── MC.Commands/                  # 命令系统
│   │   ├── MotionCommand.cs          # 命令基类
│   │   ├── MoveAbsoluteCommand.cs
│   │   ├── MoveRelativeCommand.cs
│   │   ├── HomeCommand.cs
│   │   └── CommandRegistry.cs
│   │
│   ├── MC.Tasks/                     # 任务定义（步骤枚举 + 状态机）
│   │   ├── StepDefinitions.cs        # StepA/StepB 等步骤枚举
│   │   ├── TaskA.cs                  # 任务A状态机
│   │   ├── TaskB.cs                  # 任务B状态机
│   │   └── TaskCommunication.cs      # 任务间通信
│   │
│   ├── MC.HAL/                       # 硬件抽象层
│   │   ├── IHalMotionCard.cs         # 接口定义
│   │   ├── Adapters/                 # 各厂商适配器
│   │   │   ├── LeiSaiAdapter.cs      # 雷赛
│   │   │   ├── GoogolAdapter.cs      # 固高
│   │   │   └── SimulatorAdapter.cs   # 仿真器（开发用）
│   │   └── AxisStatus.cs
│   │
│   ├── MC.Middleware/                # 中间件
│   │   ├── IMiddleware.cs
│   │   ├── LoggingMiddleware.cs
│   │   ├── SafetyMiddleware.cs
│   │   └── PerformanceMonitor.cs
│   │
│   ├── MC.Common/                    # 公共组件
│   │   ├── Enums.cs                  # 枚举定义
│   │   ├── EventArgs.cs              # 事件参数
│   │   ├── Exceptions.cs             # 自定义异常
│   │   └── TaskConfiguration.cs
│   │
│   └── MC.UI/                        # WinForm UI（可选）
│       ├── MainForm.cs               # 主窗体
│       ├── StateMonitor.cs           # 状态监控面板
│       └── LogViewer.cs              # 日志查看器
│
├── tests/
│   ├── MC.Engine.Tests/              # 引擎单元测试
│   ├── MC.Commands.Tests/            # 命令测试
│   └── MC.HAL.Tests/                 # HAL 测试
│
├── samples/
│   ├── WinFormDemo/                  # WinForm 示例程序
│   └── ConsoleDemo/                  # 控制台示例程序
│
└── docs/
    └── API.md                        # API 文档
```

---

## 九、技术栈与依赖

| 技术/库 | 版本 | 用途 |
|---------|------|------|
| **.NET 8/9** | LTS | 运行时平台 |
| **.NET Framework 4.6+** | 兼容 | 支持旧项目升级 |
| **System.Threading** | 内置 | ManualResetEventSlim / CancellationToken |
| **Microsoft.Extensions.Logging** | 8.0+ | 日志抽象 |
| **Microsoft.Extensions.DependencyInjection** | 8.0+ | 依赖注入 |
| **Polly** | 8.0+ | 重试策略（可选） |
| **xUnit** | 2.x | 单元测试框架 |
| **Moq** | 4.x | Mock 框架 |

---

## 十、关键设计模式

| 模式 | 应用场景 |
|------|----------|
| **状态机模式** | 引擎生命周期管理 + 任务步骤枚举（StepA/StepB） |
| **闸门模式** | ManualResetEvent 全局暂停/继续控制 |
| **命令模式** | 运动指令封装，支持撤销/重做 |
| **适配器模式** | 硬件抽象层，兼容不同控制卡 |
| **中间件模式** | 命令执行管道，灵活扩展 |
| **观察者模式** | 事件通知机制 |
| **策略模式** | 不同停止模式的切换 |
| **条件等待模式** | awaitCondition 任务间同步 |

---

## 十一、安全与可靠性

### 11.1 安全机制

```csharp
/// <summary>
/// 安全检查中间件示例
/// </summary>
public class SafetyMiddleware : IMiddleware
{
    private readonly SafetyLimits _limits;
    
    public async Task InvokeAsync(IExecutionContext context, Func<Task> next)
    {
        // 执行前检查
        ValidateLimits(context.CurrentCommand, _limits);
        
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            // 异常时触发急停
            await context.EmergencyStopAsync(ex);
            throw;
        }
    }
}
```

### 11.2 异常处理

```csharp
public class MotionEngineException : Exception
{
    public EngineState StateAtError { get; }
    public ErrorSeverity Severity { get; }
    
    public MotionEngineException(string message, EngineState state, ErrorSeverity severity) 
        : base(message)
    {
        StateAtError = state;
        Severity = severity;
    }
}

public enum ErrorSeverity
{
    Warning = 0,      // 警告，可继续
    Recoverable = 1,  // 可恢复错误
    Fatal = 2         // 致命错误，需要急停
}
```

---

## 十二、使用示例

### 12.1 基础使用（WinForm 按钮绑定）

```csharp
// 全局控制信号
private readonly ManualResetEventSlim _blockFlow = new(true);
private CancellationTokenSource _cts = new();
private bool _awaitCondition;

// 启动按钮
private void Start_Click(object sender, EventArgs e)
{
    if (stepA == StepA.None) stepA = StepA.StepA0;
    if (stepB == StepB.None) stepB = StepB.StepB0;
    
    _blockFlow.Set();  // 打开闸门
    Log("流程启动");
}

// 暂停按钮
private void Pause_Click(object sender, EventArgs e)
{
    _blockFlow.Reset();  // 关闭闸门
    Log("流程暂停");
}

// 继续按钮
private void Resume_Click(object sender, EventArgs e)
{
    _blockFlow.Set();  // 打开闸门
    Log("流程继续");
}

// 停止按钮
private async void Stop_Click(object sender, EventArgs e)
{
    _blockFlow.Set();  // 先释放闸门
    await _cts.CancelAsync();  // 发送取消信号
    Log("流程停止");
}
```

### 12.2 现代化 API 使用

```csharp
// 1. 配置服务
var services = new ServiceCollection();
services.AddMotionEngine(options =>
{
    options.DefaultStopMode = StopMode.Smooth;
    options.EnableMiddleware = true;
});
services.AddSingleton<IHalMotionCard, LeiSaiAdapter>();

var provider = services.BuildServiceProvider();

// 2. 获取引擎实例
var engine = provider.GetRequiredService<IMotionEngine>();

// 3. 创建任务
var task = new MotionTask
{
    Name = "取料-加工-放料",
    Commands = new List<MotionCommand>
    {
        new HomeCommand { Axis = 0 },
        new MoveAbsoluteCommand { Axis = 0, Position = 100.0, Velocity = 50.0 },
        new WaitInputCommand { InputIndex = 0, ExpectedValue = true },
        new MoveAbsoluteCommand { Axis = 0, Position = 200.0, Velocity = 30.0 },
        new MoveAbsoluteCommand { Axis = 0, Position = 0.0, Velocity = 50.0 },
    }
};

// 4. 控制引擎
engine.StateChanged += (s, e) => Console.WriteLine($"{e.PreviousState} → {e.NewState}");

await engine.StartAsync(task);

// 暂停
await engine.PauseAsync();
await Task.Delay(2000);  // 模拟人工操作
await engine.ResumeAsync();

// 停止
await engine.StopAsync(StopMode.Smooth);
```

---

## 十三、双路线对比与统一支持方案

### 13.1 两条路线对比

| 维度 | 路线A：轻量闸门模式 | 路线B：现代化引擎模式 |
|------|---------------------|------------------------|
| **核心机制** | ManualResetEvent + switch 状态机 | 状态机 + 中间件管道 + 依赖注入 |
| **代码量** | ~200 行即可实现 | ~1000+ 行 |
| **依赖** | 仅 .NET 原生类型 | Microsoft.Extensions.* 等 NuGet 包 |
| **学习成本** | 低，C# 基础即可 | 中等，需理解 DI、中间件等概念 |
| **适合框架** | WinForms / .NET Framework 4.6+ | .NET 8+ / WPF / MAUI |
| **扩展性** | 有限，新增功能需改源码 | 高，插件化注册中间件/命令 |
| **测试友好** | 较难，强耦合 UI | 好，接口抽象便于 Mock |
| **调试难度** | 低，逻辑直接可见 | 中等，中间件链需要追踪 |
| **适用场景** | 单设备、固定流程、快速交付 | 多设备、可变流程、长期维护 |
| **硬件兼容** | 需硬编码适配 | 通过 HAL 接口即插即用 |

### 13.2 优缺点详细分析

#### 路线 A：轻量闸门模式

**优点：**
- **极简**：一个 `ManualResetEventSlim` 搞定暂停/继续，`CancellationTokenSource` 搞定停止
- **零依赖**：不需要任何第三方库，.NET Framework 4.6 起即可用
- **直观**：`switch (step)` 一眼看懂流程走向，工业现场调试方便
- **交付快**：适合非标设备快速交付，改改步骤就能用

**缺点：**
- **步骤膨胀**：流程复杂后 `switch` 分支会非常长（stepA0 ~ stepA50+）
- **复用困难**：不同设备的相似流程需要复制粘贴
- **难以测试**：状态和 UI 耦合，单元测试需要先模拟 UI 环境
- **无中间件**：日志、安全监控等横切关注点需手动嵌入每个步骤

#### 路线 B：现代化引擎模式

**优点：**
- **高扩展性**：通过 `CommandRegistry` 运行时注册新命令，无需改核心代码
- **中间件链**：日志、安全、监控等通过中间件插入，职责清晰
- **易于测试**：`IMotionEngine`、`IHalMotionCard` 等接口便于 Mock
- **多设备支持**：通过 DI 切换不同 HAL 适配器，同一套引擎驱动不同硬件

**缺点：**
- **复杂度高**：状态机 + 命令模式 + 中间件 + DI，新人上手需要时间
- **过度设计风险**：简单流程用这套框架反而增加工作量
- **依赖管理**：需要维护 NuGet 包版本，旧项目升级有成本

### 13.3 统一支持方案：分层渐进式架构

本引擎**同时支持两条路线**，通过分层设计让用户按需选择：

```
┌──────────────────────────────────────────────────────────────┐
│                    路线B：现代化引擎层                         │
│  IMotionEngine + DI + 中间件管道 + CommandRegistry           │
│  ↑ 适用于：.NET 8+ 项目、多设备、长期维护产品                  │
│  │                                                            │
│  ├── AddMotionEngine() ── 一键配置                            │
│  ├── .UseMiddleware<LoggingMiddleware>()                      │
│  └── engine.StartAsync(task)                                  │
└──────────────────────────┬───────────────────────────────────┘
                           │
                    共享核心层（Common Core）
  ┌────────────────────────┼───────────────────────────────────┐
  │                        │                                    │
  │  EngineState 枚举      │  ExecutionContext                 │
  │  ManualResetEventSlim  │  StopMode 枚举                    │
  │  状态转换矩阵          │  事件系统                         │
  │                        │                                    │
  └────────────────────────┼───────────────────────────────────┘
                           │
┌──────────────────────────▼───────────────────────────────────┐
│                    路线A：轻量闸门层                           │
│  StepA/StepB 枚举 + switch 状态机 + _blockFlow               │
│  ↑ 适用于：WinForms、.NET 4.6+、快速交付项目                  │
│  │                                                            │
  ├── Start_Click → _blockFlow.Set()                           │
  ├── Pause_Click → _blockFlow.Reset()                         │
  └── switch (stepA) { case StepA0: ... }                      │
└──────────────────────────────────────────────────────────────┘
```

### 13.4 两种使用方式

#### 方式一：轻量模式（路线 A）

```csharp
// 适合 WinForms，直接引用 MC.Engine 项目
public partial class MainForm : Form
{
    private readonly ManualResetEventSlim _blockFlow = new(true);
    private CancellationTokenSource _cts = new();
    private StepA _stepA = StepA.None;
    private StepB _stepB = StepB.None;
    
    private void Start_Click(object sender, EventArgs e)
    {
        if (_stepA == StepA.None) _stepA = StepA.StepA0;
        _blockFlow.Set();
        _ = Task.Run(() => RunTaskA(_cts.Token));
    }
    
    private void Pause_Click(object sender, EventArgs e) => _blockFlow.Reset();
    private void Resume_Click(object sender, EventArgs e) => _blockFlow.Set();
    private void Stop_Click(object sender, EventArgs e) 
    { 
        _blockFlow.Set(); 
        _cts.Cancel(); 
    }
    
    private async Task RunTaskA(CancellationToken ct)
    {
        while (_stepA != StepA.None && !ct.IsCancellationRequested)
        {
            _blockFlow.Wait(ct);  // 暂停闸门
            
            switch (_stepA)
            {
                case StepA.StepA0:
                    await MoveAxis(0, 0);
                    _stepA = StepA.StepA1;
                    break;
                case StepA.StepA1:
                    await MoveAxis(0, 100);
                    _stepA = StepA.StepA2;
                    break;
                // ... 更多步骤
            }
            
            Log($"TaskA: {_stepA}");
        }
    }
}
```

#### 方式二：完整模式（路线 B）

```csharp
// 适合 .NET 8+ 项目，通过 NuGet 引用
var services = new ServiceCollection();

// 一键配置引擎
services.AddMotionEngine(options =>
{
    options.DefaultStopMode = StopMode.Smooth;
    options.EnableMiddleware = true;
})
.UseMiddleware<LoggingMiddleware>()      // 日志中间件
.UseMiddleware<SafetyMiddleware>()       // 安全中间件
.UseMiddleware<PerformanceMonitor>();    // 性能监控中间件

// 注册硬件适配器
services.AddSingleton<IHalMotionCard, LeiSaiAdapter>();

var provider = services.BuildServiceProvider();
var engine = provider.GetRequiredService<IMotionEngine>();

// 订阅事件
engine.StateChanged += (s, e) => 
    Console.WriteLine($"{e.PreviousState} → {e.NewState}");

// 启动任务
await engine.StartAsync(new MotionTask
{
    Name = "取料-加工-放料",
    Commands = new List<MotionCommand>
    {
        new HomeCommand { Axis = 0 },
        new MoveAbsoluteCommand { Axis = 0, Position = 100.0 },
        new WaitInputCommand { InputIndex = 0, ExpectedValue = true },
        new MoveAbsoluteCommand { Axis = 0, Position = 0.0 },
    }
});

// 暂停/继续/停止
await engine.PauseAsync();
await engine.ResumeAsync();
await engine.StopAsync(StopMode.Smooth);
```

### 13.5 渐进式升级路径

```
项目初期                              项目成熟期
┌─────────────────┐                  ┌─────────────────┐
│  路线A：轻量模式  │ ── 逐步迁移 ──→ │  路线B：完整模式  │
│                 │                  │                 │
│  • switch 状态机│                  │  • Command 模式  │
│  • 手动暂停控制 │                  │  • 中间件管道    │
│  • 硬编码 HAL   │                  │  • DI 适配器     │
│  • WinForms UI  │                  │  • 抽象接口      │
└─────────────────┘                  └─────────────────┘

迁移步骤：
1. 将 switch 步骤抽成 MotionCommand 子类
2. 将 ManualResetEvent 包装进 ExecutionContext
3. 引入 DI 容器，替换硬编码依赖
4. 按需添加中间件
```

---

## 十四、后续扩展方向

1. **轨迹插补**：支持直线/圆弧插补，多轴联动
2. **电子凸轮**：实现复杂的轴间同步运动
3. **任务编排器**：支持多任务并行/依赖关系
4. **远程监控**：通过 WebSocket/MQTT 实现远程状态监控
5. **数据持久化**：运动数据记录与分析
6. **可视化编辑器**：拖拽式任务流程编辑器

---

> **文档版本**: v1.1
> **创建日期**: 2026-05-09
> **更新日期**: 2026-05-09
> **作者**: Qwen Code
