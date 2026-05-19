using MC.Engine.Core;
using Microsoft.Extensions.Logging;

namespace MC.Engine.Middleware;

/// <summary>
/// 中间件接口
/// </summary>
public interface IMiddleware
{
    Task InvokeAsync(IExecutionContext context, Func<Task> next);
}

/// <summary>
/// 日志中间件
/// </summary>
public class LoggingMiddleware : IMiddleware
{
    private readonly ILogger<LoggingMiddleware> _logger;

    public LoggingMiddleware(ILogger<LoggingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(IExecutionContext context, Func<Task> next)
    {
        _logger.LogInformation("开始执行命令: {CommandName}", context.CurrentCommand?.Name);

        var start = DateTime.UtcNow;
        try
        {
            await next();
            var elapsed = DateTime.UtcNow - start;
            _logger.LogInformation("命令执行完成: {CommandName}, 耗时: {ElapsedMs}ms",
                context.CurrentCommand?.Name, elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "命令执行失败: {CommandName}", context.CurrentCommand?.Name);
            throw;
        }
    }
}

/// <summary>
/// 安全检查中间件
/// </summary>
public class SafetyMiddleware : IMiddleware
{
    public async Task InvokeAsync(IExecutionContext context, Func<Task> next)
    {
        // 执行前检查
        if (context.CurrentCommand == null)
            throw new InvalidOperationException("当前命令为空");

        try
        {
            await next();
        }
        catch (Exception ex)
        {
            // 异常时可以触发急停
            context.RequestStop();
            throw;
        }
    }
}

/// <summary>
/// 性能监控中间件
/// </summary>
public class PerformanceMonitorMiddleware : IMiddleware
{
    private readonly ILogger<PerformanceMonitorMiddleware> _logger;

    public PerformanceMonitorMiddleware(ILogger<PerformanceMonitorMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(IExecutionContext context, Func<Task> next)
    {
        var start = DateTime.UtcNow;
        await next();
        var elapsed = DateTime.UtcNow - start;

        if (elapsed.TotalMilliseconds > 1000)
        {
            _logger.LogWarning("命令执行耗时较长: {CommandName}, 耗时: {ElapsedMs}ms",
                context.CurrentCommand?.Name, elapsed.TotalMilliseconds);
        }
    }
}
