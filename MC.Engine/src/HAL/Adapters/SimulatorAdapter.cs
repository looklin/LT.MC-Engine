using MC.Engine.Common;
using MC.Engine.HAL;

namespace MC.Engine.HAL.Adapters;

/// <summary>
/// 仿真器适配器：用于开发和测试，不连接真实硬件
/// </summary>
public class SimulatorAdapter : IHalMotionCard
{
    private readonly Dictionary<int, AxisStatus> _axisStatus = new();
    private readonly Random _random = new();
    private bool _disposed;

    public SimulatorAdapter()
    {
        // 初始化 8 个轴
        for (int i = 0; i < 8; i++)
        {
            _axisStatus[i] = new AxisStatus
            {
                IsEnabled = true,
                IsMoving = false,
                CurrentVelocity = 0,
                CurrentPosition = 0,
                IsInError = false
            };
        }
    }

    public async Task MoveAxisAsync(int axisIndex, double targetPosition, double velocity, CancellationToken ct)
    {
        if (!_axisStatus.ContainsKey(axisIndex))
            throw new ArgumentException($"轴 {axisIndex} 不存在");

        _axisStatus[axisIndex] = _axisStatus[axisIndex] with
        {
            IsMoving = true,
            CurrentVelocity = velocity
        };

        // 模拟运动延迟
        var delay = (int)(Math.Abs(targetPosition - _axisStatus[axisIndex].CurrentPosition) / velocity * 1000);
        delay = Math.Max(100, Math.Min(delay, 2000));

        await Task.Delay(delay, ct);

        _axisStatus[axisIndex] = _axisStatus[axisIndex] with
        {
            IsMoving = false,
            CurrentVelocity = 0,
            CurrentPosition = targetPosition
        };
    }

    public Task StopAxisAsync(int axisIndex, StopMode mode, CancellationToken ct)
    {
        if (!_axisStatus.ContainsKey(axisIndex))
            throw new ArgumentException($"轴 {axisIndex} 不存在");

        _axisStatus[axisIndex] = _axisStatus[axisIndex] with
        {
            IsMoving = false,
            CurrentVelocity = 0
        };

        return Task.CompletedTask;
    }

    public Task EmergencyStopAllAsync(CancellationToken ct)
    {
        foreach (var key in _axisStatus.Keys.ToList())
        {
            _axisStatus[key] = _axisStatus[key] with
            {
                IsMoving = false,
                CurrentVelocity = 0
            };
        }

        return Task.CompletedTask;
    }

    public Task<AxisStatus> GetAxisStatusAsync(int axisIndex, CancellationToken ct)
    {
        if (!_axisStatus.ContainsKey(axisIndex))
            throw new ArgumentException($"轴 {axisIndex} 不存在");

        return Task.FromResult(_axisStatus[axisIndex]);
    }

    public Task<double[]> GetCurrentPositionAsync(CancellationToken ct)
    {
        var positions = _axisStatus.Values.Select(s => s.CurrentPosition).ToArray();
        return Task.FromResult(positions);
    }

    public Task<bool> ReadInputAsync(int inputIndex, CancellationToken ct)
    {
        // 模拟输入信号
        return Task.FromResult(_random.Next(0, 2) == 1);
    }

    public Task WriteOutputAsync(int outputIndex, bool value, CancellationToken ct)
    {
        // 模拟输出信号
        return Task.CompletedTask;
    }

    public event EventHandler<HardwareErrorEventArgs>? HardwareError;

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
