using System;

namespace MC.Engine.Light
{
    /// <summary>
    /// 运动引擎异常
    /// </summary>
    public class MotionEngineException : Exception
    {
        /// <summary>错误发生时的引擎状态</summary>
        public EngineState StateAtError { get; }

        /// <summary>错误严重级别</summary>
        public ErrorSeverity Severity { get; }

        public MotionEngineException(string message) : base(message)
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
}
