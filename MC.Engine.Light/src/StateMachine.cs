/// <summary>
/// 线程安全的状态机管理器
/// 
/// 设计要点：
/// 1. 使用锁保证状态转换的原子性，防止并发调用导致状态错乱
/// 2. 状态转换遵循预定义的转换矩阵，非法转换会被拒绝
/// 3. 每次转换成功后自动触发 StateChanged 事件
/// </summary>

using System;
using System.Collections.Generic;

namespace MC.Engine.Light
{
    /// <summary>
    /// 状态机：管理引擎生命周期中的状态转换
    /// </summary>
    public class StateMachine
    {
        /// <summary>当前状态，初始为 Idle</summary>
        private EngineState _currentState = EngineState.Idle;

        /// <summary>状态转换锁：保证并发安全</summary>
        private readonly object _lock = new object();

        /// <summary>
        /// 当前状态（只读）
        /// </summary>
        public EngineState CurrentState
        {
            get => _currentState;
            private set => _currentState = value;
        }

        /// <summary>
        /// 状态转换规则矩阵
        /// 格式：(当前状态, 输入命令) => 下一状态
        /// 
        /// 转换流程：
        /// Idle --Start--> Running --Pause--> Paused --Resume--> Running
        ///                        \--Stop--> Stopping --StopComplete--> Idle
        /// Error --Reset--> Idle
        /// </summary>
        private static readonly Dictionary<(EngineState, EngineCommand), EngineState> TransitionRules =
            new Dictionary<(EngineState, EngineCommand), EngineState>
            {
                // 空闲状态：只能启动
                { (EngineState.Idle, EngineCommand.Start), EngineState.Running },

                // 运行状态：可暂停或停止
                { (EngineState.Running, EngineCommand.Pause), EngineState.Paused },
                { (EngineState.Running, EngineCommand.Stop), EngineState.Stopping },

                // 暂停状态：可继续或停止
                { (EngineState.Paused, EngineCommand.Resume), EngineState.Running },
                { (EngineState.Paused, EngineCommand.Stop), EngineState.Stopping },

                // 停止中：只能完成停止
                { (EngineState.Stopping, EngineCommand.StopComplete), EngineState.Idle },

                // 错误状态：只能复位
                { (EngineState.Error, EngineCommand.Reset), EngineState.Idle },
            };

        /// <summary>状态变更事件：外部可订阅以响应状态变化</summary>
        public event EventHandler<StateChangedEventArgs>? StateChanged;

        /// <summary>
        /// 尝试执行状态转换
        /// </summary>
        /// <param name="command">要执行的命令</param>
        /// <returns>是否转换成功</returns>
        public bool TryTransition(EngineCommand command)
        {
            lock (_lock)
            {
                var key = (CurrentState, command);
                if (TransitionRules.TryGetValue(key, out var nextState))
                {
                    var previous = CurrentState;
                    CurrentState = nextState;
                    StateChanged?.Invoke(this, new StateChangedEventArgs(previous, nextState));
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 强制设置状态（用于初始化或错误恢复）
        /// </summary>
        public void ForceState(EngineState newState)
        {
            lock (_lock)
            {
                var previous = CurrentState;
                CurrentState = newState;
                StateChanged?.Invoke(this, new StateChangedEventArgs(previous, newState));
            }
        }
    }
}
