namespace MC.Engine.Common;

/// <summary>
/// 运动引擎异常
/// </summary>
public class MotionEngineException : Exception
{
    public EngineState StateAtError { get; }
    public ErrorSeverity Severity { get; }

    public MotionEngineException(string message)
        : base(message)
    {
        StateAtError = EngineState.Error;
        Severity = ErrorSeverity.Recoverable;
    }

    public MotionEngineException(string message, EngineState stateAtError, ErrorSeverity severity)
        : base(message)
    {
        StateAtError = stateAtError;
        Severity = severity;
    }
}

/// <summary>
/// 无效状态异常
/// </summary>
public class InvalidStateException : MotionEngineException
{
    public EngineState CurrentState { get; }
    public EngineCommand AttemptedCommand { get; }

    public InvalidStateException(EngineState currentState, EngineCommand command)
        : base($"状态 {currentState} 下不允许执行 {command}")
    {
        CurrentState = currentState;
        AttemptedCommand = command;
    }
}
