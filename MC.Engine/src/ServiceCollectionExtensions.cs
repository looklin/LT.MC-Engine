using MC.Engine.Common;
using MC.Engine.Core;
using MC.Engine.HAL;
using MC.Engine.HAL.Adapters;
using MC.Engine.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace MC.Engine;

/// <summary>
/// 依赖注入扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMotionEngine(
        this IServiceCollection services,
        Action<MotionEngineOptions>? configure = null)
    {
        var options = new MotionEngineOptions();
        configure?.Invoke(options);

        // 注册硬件抽象层
        services.AddSingleton(options.HalFactory?.Invoke(services.BuildServiceProvider()) 
            ?? new SimulatorAdapter());

        // 注册中间件
        if (options.EnableMiddleware)
        {
            foreach (var middlewareType in options.MiddlewareTypes)
            {
                services.AddTransient(typeof(IMiddleware), middlewareType);
            }
        }

        // 注册引擎
        services.AddSingleton<IMotionEngine, MotionEngine>();

        return services;
    }

    public static IServiceCollection AddMiddleware<T>(this IServiceCollection services)
        where T : class, IMiddleware
    {
        services.AddTransient<IMiddleware, T>();
        return services;
    }
}

/// <summary>
/// 引擎配置选项
/// </summary>
public class MotionEngineOptions
{
    /// <summary>默认停止模式</summary>
    public StopMode DefaultStopMode { get; set; } = StopMode.Smooth;

    /// <summary>启用中间件</summary>
    public bool EnableMiddleware { get; set; } = true;

    /// <summary>中间件类型列表</summary>
    public List<Type> MiddlewareTypes { get; } = new();

    /// <summary>硬件抽象层工厂函数</summary>
    public Func<IServiceProvider, IHalMotionCard>? HalFactory { get; set; }
}
