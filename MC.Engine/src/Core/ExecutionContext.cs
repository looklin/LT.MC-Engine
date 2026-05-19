using MC.Engine.Core;

namespace MC.Engine.Core;

/// <summary>
/// 执行上下文实现
/// </summary>
public class ExecutionContext : IExecutionContext, IDisposable
{
    private readonly ManualResetEventSlim _runGate = new(true);
    private volatile bool _isStopping;
    private readonly CancellationTokenSource _cts = new();
    private readonly Func<double, Task>? _progressReporter;

    public CancellationToken CancellationToken => _cts.Token;
    public MotionCommand? CurrentCommand { get; internal set; }

    public ExecutionContext(Func<double, Task>? progressReporter = null)
    {
        _progressReporter = progressReporter;
    }

    public async Task WaitIfPausedAsync(CancellationToken ct = default)
    {
        if (_isStopping)
        {
            throw new OperationCanceledException("停止请求已发出", _cts.Token);
        }

        // 使用 linked token 支持外部取消
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, ct);
        await Task.Run(() => _runGate.Wait(linkedCts.Token), linkedCts.Token);
    }

    public void RequestPause() => _runGate.Reset();

    public void RequestResume() => _runGate.Set();

    public void RequestStop()
    {
        _isStopping = true;
        _runGate.Set();
        _cts.Cancel();
    }

    public async Task ReportProgressAsync(double progress)
    {
        if (_progressReporter != null)
        {
            await _progressReporter(progress);
        }
    }

    public void Dispose()
    {
        _runGate.Dispose();
        _cts.Dispose();
    }
}
