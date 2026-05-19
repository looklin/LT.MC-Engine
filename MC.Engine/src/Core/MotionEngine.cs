using MC.Engine.Common;
using MC.Engine.HAL;
using MC.Engine.Middleware;
using Microsoft.Extensions.Logging;

namespace MC.Engine.Core;

/// <summary>
/// 完整功能运动控制引擎
/// 支持中间件管道、依赖注入、硬件抽象层
/// </summary>
public class MotionEngine : IMotionEngine
{
    private readonly StateMachine _stateMachine;
    private readonly IHalMotionCard _hal;
    private readonly IEnumerable<IMiddleware> _middlewares;
    private readonly ILogger<MotionEngine> _logger;
    private ExecutionContext? _context;
    private readonly object _lock = new();
    private MotionTask? _currentTask;
    private double _currentProgress;

    public EngineState State => _stateMachine.CurrentState;
    public MotionTask? CurrentTask => _currentTask;
    public double CurrentProgress => _currentProgress;

    public event EventHandler<StateChangedEventArgs>? StateChanged;
    public event EventHandler<CommandProgressEventArgs>? ProgressChanged;
    public event EventHandler<MC.Engine.Common.ErrorEventArgs>? ErrorOccurred;

    public MotionEngine(
        IHalMotionCard hal,
        IEnumerable<IMiddleware>? middlewares = null,
        ILogger<MotionEngine>? logger = null)
    {
        _hal = hal;
        _middlewares = middlewares ?? Enumerable.Empty<IMiddleware>();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MotionEngine>.Instance;

        _stateMachine = new StateMachine();
        _stateMachine.StateChanged += (s, e) => StateChanged?.Invoke(this, e);
    }

    public async Task StartAsync(MotionTask task, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (State != EngineState.Idle && State != EngineState.Error)
                throw new InvalidStateException(State, EngineCommand.Start);
        }

        if (!_stateMachine.TryTransition(EngineCommand.Start))
            throw new InvalidOperationException("无法启动引擎");

        _currentTask = task;
        _currentProgress = 0;

        // 创建执行上下文
        _context = new ExecutionContext(async progress =>
        {
            _currentProgress = progress;
            ProgressChanged?.Invoke(this, new CommandProgressEventArgs(
                _context?.CurrentCommand?.Id ?? string.Empty, progress));
        });

        try
        {
            // 按顺序执行所有命令
            for (int i = 0; i < task.Commands.Count; i++)
            {
                var command = task.Commands[i];
                _context.CurrentCommand = command;

                // 通过中间件管道执行
                await ExecuteWithMiddleware(command, _context);

                // 更新总进度
                _currentProgress = (double)(i + 1) / task.Commands.Count;
            }

            // 任务完成
            _stateMachine.TryTransition(EngineCommand.StopComplete);
            _logger.LogInformation("任务完成: {TaskName}", task.Name);
        }
        catch (OperationCanceledException) when (_context.CancellationToken.IsCancellationRequested)
        {
            _stateMachine.TryTransition(EngineCommand.StopComplete);
            _logger.LogInformation("任务已停止: {TaskName}", task.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "任务执行失败: {TaskName}", task.Name);
            OnError(ex.Message, ErrorSeverity.Fatal);
            _stateMachine.ForceState(EngineState.Error);
            throw;
        }
        finally
        {
            _context?.Dispose();
            _context = null;
            _currentTask = null;
        }
    }

    public Task PauseAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (State != EngineState.Running)
                throw new InvalidStateException(State, EngineCommand.Pause);
        }

        _context?.RequestPause();
        _stateMachine.TryTransition(EngineCommand.Pause);
        _logger.LogInformation("引擎已暂停");
        return Task.CompletedTask;
    }

    public Task ResumeAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (State != EngineState.Paused)
                throw new InvalidStateException(State, EngineCommand.Resume);
        }

        _context?.RequestResume();
        _stateMachine.TryTransition(EngineCommand.Resume);
        _logger.LogInformation("引擎已恢复");
        return Task.CompletedTask;
    }

    public Task StopAsync(StopMode mode = StopMode.Smooth, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (State != EngineState.Running && State != EngineState.Paused)
                throw new InvalidStateException(State, EngineCommand.Stop);
        }

        _context?.RequestStop();
        _stateMachine.TryTransition(EngineCommand.Stop);
        _logger.LogInformation("引擎正在停止: {Mode}", mode);
        return Task.CompletedTask;
    }

    public Task ResetAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (State != EngineState.Error)
                throw new InvalidStateException(State, EngineCommand.Reset);
        }

        _stateMachine.TryTransition(EngineCommand.Reset);
        _logger.LogInformation("引擎已复位");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 通过中间件管道执行命令
    /// </summary>
    private async Task ExecuteWithMiddleware(MotionCommand command, ExecutionContext context)
    {
        // 构建中间件管道
        Func<Task> pipeline = async () =>
        {
            await command.PrepareAsync(context);
            await command.ExecuteAsync(context);
            await command.CleanupAsync(context);
        };

        // 反向包装中间件（最后一个中间件先执行）
        var middlewareList = _middlewares.ToList();
        for (int i = middlewareList.Count - 1; i >= 0; i--)
        {
            var middleware = middlewareList[i];
            var next = pipeline;
            pipeline = async () => await middleware.InvokeAsync(context, next);
        }

        await pipeline();
    }

    protected void OnError(string message, ErrorSeverity severity)
    {
        ErrorOccurred?.Invoke(this, new MC.Engine.Common.ErrorEventArgs(message, severity));
    }

    public void Dispose()
    {
        _context?.Dispose();
        _hal.Dispose();
    }
}
