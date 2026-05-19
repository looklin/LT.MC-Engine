using MC.Engine.Common;

namespace MC.Engine.Core;

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
    event EventHandler<StateChangedEventArgs> StateChanged;
    event EventHandler<CommandProgressEventArgs> ProgressChanged;
    event EventHandler<MC.Engine.Common.ErrorEventArgs> ErrorOccurred;
}

/// <summary>
/// 执行上下文接口
/// </summary>
public interface IExecutionContext
{
    CancellationToken CancellationToken { get; }
    MotionCommand? CurrentCommand { get; }
    
    Task WaitIfPausedAsync(CancellationToken ct = default);
    void RequestPause();
    void RequestResume();
    void RequestStop();
    Task ReportProgressAsync(double progress);
}

/// <summary>
/// 运动任务
/// </summary>
public class MotionTask
{
    public string TaskId { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public IList<MotionCommand> Commands { get; set; } = new List<MotionCommand>();
    public TaskConfiguration Config { get; set; } = new();
}

/// <summary>
/// 运动命令基类
/// </summary>
public abstract class MotionCommand
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;

    /// <summary>执行前的准备动作</summary>
    public abstract Task PrepareAsync(IExecutionContext context);

    /// <summary>核心执行逻辑</summary>
    public abstract Task ExecuteAsync(IExecutionContext context);

    /// <summary>执行后的清理</summary>
    public abstract Task CleanupAsync(IExecutionContext context);
}
