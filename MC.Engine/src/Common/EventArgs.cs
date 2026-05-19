namespace MC.Engine.Common;

/// <summary>
/// 状态变更事件参数
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
/// 错误事件参数
/// </summary>
public class ErrorEventArgs : EventArgs
{
    public string ErrorMessage { get; }
    public ErrorSeverity Severity { get; }
    public DateTime Timestamp { get; }

    public ErrorEventArgs(string message, ErrorSeverity severity)
    {
        ErrorMessage = message;
        Severity = severity;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// 命令进度事件参数
/// </summary>
public class CommandProgressEventArgs : EventArgs
{
    public string CommandId { get; }
    public double Progress { get; } // 0.0 ~ 1.0

    public CommandProgressEventArgs(string commandId, double progress)
    {
        CommandId = commandId;
        Progress = progress;
    }
}
