using MC.Engine.Common;

namespace MC.Engine.HAL;

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
    event EventHandler<HardwareErrorEventArgs> HardwareError;
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

/// <summary>
/// 硬件错误事件参数
/// </summary>
public class HardwareErrorEventArgs : EventArgs
{
    public string ErrorMessage { get; }
    public int? AxisIndex { get; }
    public DateTime Timestamp { get; }

    public HardwareErrorEventArgs(string message, int? axisIndex = null)
    {
        ErrorMessage = message;
        AxisIndex = axisIndex;
        Timestamp = DateTime.UtcNow;
    }
}
