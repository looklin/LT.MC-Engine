namespace MC.Engine.Common;

/// <summary>
/// 引擎状态
/// </summary>
public enum EngineState
{
    /// <summary>空闲状态，等待启动</summary>
    Idle = 0,

    /// <summary>正在运行</summary>
    Running = 1,

    /// <summary>已暂停</summary>
    Paused = 2,

    /// <summary>正在停止</summary>
    Stopping = 3,

    /// <summary>错误状态，需要复位</summary>
    Error = 4
}

/// <summary>
/// 引擎命令
/// </summary>
public enum EngineCommand
{
    /// <summary>启动</summary>
    Start,

    /// <summary>暂停</summary>
    Pause,

    /// <summary>继续</summary>
    Resume,

    /// <summary>停止</summary>
    Stop,

    /// <summary>急停</summary>
    EmergencyStop,

    /// <summary>停止完成</summary>
    StopComplete,

    /// <summary>复位</summary>
    Reset
}

/// <summary>
/// 停止模式
/// </summary>
public enum StopMode
{
    /// <summary>平滑停止：减速到零</summary>
    Smooth = 0,

    /// <summary>快速停止：按最大减速度停止</summary>
    Quick = 1,

    /// <summary>急停：立即切断动力</summary>
    Emergency = 2
}

/// <summary>
/// 错误严重级别
/// </summary>
public enum ErrorSeverity
{
    /// <summary>警告，可继续运行</summary>
    Warning = 0,

    /// <summary>可恢复错误</summary>
    Recoverable = 1,

    /// <summary>致命错误，需要停止</summary>
    Fatal = 2
}

/// <summary>
/// 任务配置
/// </summary>
public class TaskConfiguration
{
    /// <summary>默认停止模式</summary>
    public StopMode DefaultStopMode { get; set; } = StopMode.Smooth;

    /// <summary>启用中间件</summary>
    public bool EnableMiddleware { get; set; } = true;

    /// <summary>命令执行超时（毫秒），0 表示不超时</summary>
    public int CommandTimeoutMs { get; set; } = 30000;
}
