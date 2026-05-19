/// <summary>
/// 运动控制引擎公共类型定义
/// 包含引擎状态、命令、停止模式和错误级别等核心枚举
/// </summary>

using System;

namespace MC.Engine.Light
{
    /// <summary>
    /// 引擎运行状态
    /// 状态转换遵循严格的PLC模式：Idle → Running → Paused → Running/Idle
    /// </summary>
    public enum EngineState
    {
        /// <summary>空闲状态：引擎已初始化，等待启动命令</summary>
        Idle = 0,

        /// <summary>运行状态：任务正在执行中</summary>
        Running = 1,

        /// <summary>暂停状态：任务已挂起，保持当前步骤等待继续</summary>
        Paused = 2,

        /// <summary>停止中：正在执行减速停止流程</summary>
        Stopping = 3,

        /// <summary>错误状态：发生异常，需要手动复位才能重新启动</summary>
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
}
