using MC.Engine;
using MC.Engine.Commands;
using MC.Engine.Common;
using MC.Engine.Core;
using MC.Engine.HAL;
using MC.Engine.HAL.Adapters;
using MC.Engine.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MC.Engine.Demo;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("  MC Engine - 完整功能演示");
        Console.WriteLine("========================================\n");

        // 配置服务
        var services = new ServiceCollection();

        // 添加日志
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // 添加引擎，配置中间件
        services.AddMotionEngine(options =>
        {
            options.DefaultStopMode = StopMode.Smooth;
            options.EnableMiddleware = true;
            options.MiddlewareTypes.Add(typeof(LoggingMiddleware));
            options.MiddlewareTypes.Add(typeof(SafetyMiddleware));
            options.MiddlewareTypes.Add(typeof(PerformanceMonitorMiddleware));
            options.HalFactory = _ => new SimulatorAdapter();
        });

        var provider = services.BuildServiceProvider();

        // 获取引擎实例
        var engine = provider.GetRequiredService<IMotionEngine>();

        // 订阅事件
        engine.StateChanged += (s, e) =>
        {
            Console.WriteLine($"[事件] 状态变更: {e.PreviousState} → {e.NewState}");
        };

        engine.ProgressChanged += (s, e) =>
        {
            Console.WriteLine($"[事件] 进度: {e.CommandId[..8]}... = {e.Progress:P0}");
        };

        engine.ErrorOccurred += (s, e) =>
        {
            Console.WriteLine($"[事件] 错误: {e.ErrorMessage} [{e.Severity}]");
        };

        // 创建任务
        var hal = provider.GetRequiredService<IHalMotionCard>();
        var task = new MotionTask
        {
            Name = "取料-加工-放料",
            Commands = new List<MotionCommand>
            {
                new HomeCommand(hal) { Axis = 0 },
                new MoveAbsoluteCommand(hal) { Axis = 0, Position = 100.0, Velocity = 50.0, Name = "移动到取料位" },
                new DwellCommand { DurationMs = 500, Name = "取料等待" },
                new MoveAbsoluteCommand(hal) { Axis = 0, Position = 200.0, Velocity = 30.0, Name = "移动到加工位" },
                new DwellCommand { DurationMs = 1000, Name = "加工操作" },
                new MoveAbsoluteCommand(hal) { Axis = 0, Position = 0.0, Velocity = 50.0, Name = "返回原点" },
            }
        };

        // 启动任务
        Console.WriteLine("\n>>> 按 Enter 启动任务...");
        Console.ReadLine();

        var taskRun = Task.Run(async () =>
        {
            try
            {
                await engine.StartAsync(task);
                Console.WriteLine("\n>>> 任务完成!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n>>> 任务失败: {ex.Message}");
            }
        });

        // 演示暂停/继续
        Console.WriteLine("\n>>> 3 秒后暂停...");
        await Task.Delay(3000);
        Console.WriteLine(">>> 暂停任务");
        await engine.PauseAsync();

        Console.WriteLine("\n>>> 暂停 2 秒...");
        await Task.Delay(2000);

        Console.WriteLine("\n>>> 继续任务");
        await engine.ResumeAsync();

        // 等待任务完成或用户停止
        Console.WriteLine("\n>>> 任务继续运行，按 Enter 手动停止...");
        var inputTask = Task.Run(() => Console.ReadLine());
        var completedTask = await Task.WhenAny(taskRun, inputTask);

        if (completedTask == inputTask)
        {
            Console.WriteLine(">>> 手动停止任务");
            await engine.StopAsync(StopMode.Smooth);
        }

        await taskRun;

        Console.WriteLine("\n演示完成！按任意键退出...");
        Console.ReadKey();
    }
}
