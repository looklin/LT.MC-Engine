using System;

namespace MC.Engine.Light
{
    /// <summary>
    /// 状态变更事件参数
    /// </summary>
    public class StateChangedEventArgs : EventArgs
    {
        /// <summary>之前的状态</summary>
        public EngineState PreviousState { get; }

        /// <summary>新的状态</summary>
        public EngineState NewState { get; }

        /// <summary>时间戳</summary>
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
        /// <summary>错误消息</summary>
        public string ErrorMessage { get; }

        /// <summary>错误严重级别</summary>
        public ErrorSeverity Severity { get; }

        /// <summary>时间戳</summary>
        public DateTime Timestamp { get; }

        public ErrorEventArgs(string message, ErrorSeverity severity)
        {
            ErrorMessage = message;
            Severity = severity;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 步骤变更事件参数
    /// </summary>
    public class StepChangedEventArgs : EventArgs
    {
        /// <summary>任务名称</summary>
        public string TaskName { get; }

        /// <summary>之前的步骤</summary>
        public string PreviousStep { get; }

        /// <summary>当前步骤</summary>
        public string CurrentStep { get; }

        /// <summary>时间戳</summary>
        public DateTime Timestamp { get; }

        public StepChangedEventArgs(string taskName, string previousStep, string currentStep)
        {
            TaskName = taskName;
            PreviousStep = previousStep;
            CurrentStep = currentStep;
            Timestamp = DateTime.UtcNow;
        }
    }
}
