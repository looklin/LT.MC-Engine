using MC.Engine.Core;
using MC.Engine.HAL;

namespace MC.Engine.Commands;

/// <summary>
/// 绝对位置移动命令
/// </summary>
public class MoveAbsoluteCommand : MotionCommand
{
    public int Axis { get; set; }
    public double Position { get; set; }
    public double Velocity { get; set; } = 50.0;

    private readonly IHalMotionCard _hal;

    public MoveAbsoluteCommand(IHalMotionCard hal)
    {
        _hal = hal;
        Name = $"MoveAbsolute(Axis={Axis}, Pos={Position})";
    }

    public override Task PrepareAsync(IExecutionContext context)
    {
        // 可以在这里做减速等准备工作
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync(IExecutionContext context)
    {
        await _hal.MoveAxisAsync(Axis, Position, Velocity, context.CancellationToken);
        await context.ReportProgressAsync(1.0);
    }

    public override Task CleanupAsync(IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// 相对位置移动命令
/// </summary>
public class MoveRelativeCommand : MotionCommand
{
    public int Axis { get; set; }
    public double Distance { get; set; }
    public double Velocity { get; set; } = 50.0;

    private readonly IHalMotionCard _hal;

    public MoveRelativeCommand(IHalMotionCard hal)
    {
        _hal = hal;
        Name = $"MoveRelative(Axis={Axis}, Dist={Distance})";
    }

    public override async Task PrepareAsync(IExecutionContext context)
    {
        var status = await _hal.GetAxisStatusAsync(Axis, context.CancellationToken);
        // 基于当前位置计算目标
    }

    public override async Task ExecuteAsync(IExecutionContext context)
    {
        var status = await _hal.GetAxisStatusAsync(Axis, context.CancellationToken);
        var targetPosition = status.CurrentPosition + Distance;
        await _hal.MoveAxisAsync(Axis, targetPosition, Velocity, context.CancellationToken);
        await context.ReportProgressAsync(1.0);
    }

    public override Task CleanupAsync(IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// 回零命令
/// </summary>
public class HomeCommand : MotionCommand
{
    public int Axis { get; set; }

    private readonly IHalMotionCard _hal;

    public HomeCommand(IHalMotionCard hal)
    {
        _hal = hal;
        Name = $"Home(Axis={Axis})";
    }

    public override Task PrepareAsync(IExecutionContext context) => Task.CompletedTask;

    public override async Task ExecuteAsync(IExecutionContext context)
    {
        await _hal.MoveAxisAsync(Axis, 0, 30.0, context.CancellationToken);
        await context.ReportProgressAsync(1.0);
    }

    public override Task CleanupAsync(IExecutionContext context) => Task.CompletedTask;
}

/// <summary>
/// 等待命令（延时）
/// </summary>
public class DwellCommand : MotionCommand
{
    public int DurationMs { get; set; } = 1000;

    public override Task PrepareAsync(IExecutionContext context) => Task.CompletedTask;

    public override async Task ExecuteAsync(IExecutionContext context)
    {
        await Task.Delay(DurationMs, context.CancellationToken);
        await context.ReportProgressAsync(1.0);
    }

    public override Task CleanupAsync(IExecutionContext context) => Task.CompletedTask;
}

/// <summary>
/// 等待输入信号命令
/// </summary>
public class WaitInputCommand : MotionCommand
{
    public int InputIndex { get; set; }
    public bool ExpectedValue { get; set; } = true;
    public int TimeoutMs { get; set; } = 10000;

    private readonly IHalMotionCard _hal;

    public WaitInputCommand(IHalMotionCard hal)
    {
        _hal = hal;
        Name = $"WaitInput(Input={InputIndex}, Expected={ExpectedValue})";
    }

    public override Task PrepareAsync(IExecutionContext context) => Task.CompletedTask;

    public override async Task ExecuteAsync(IExecutionContext context)
    {
        var start = DateTime.UtcNow;
        while (true)
        {
            var value = await _hal.ReadInputAsync(InputIndex, context.CancellationToken);
            if (value == ExpectedValue)
                break;

            if ((DateTime.UtcNow - start).TotalMilliseconds > TimeoutMs)
                throw new TimeoutException($"等待输入 {InputIndex} 超时");

            await Task.Delay(50, context.CancellationToken);
        }

        await context.ReportProgressAsync(1.0);
    }

    public override Task CleanupAsync(IExecutionContext context) => Task.CompletedTask;
}
