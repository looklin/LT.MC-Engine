using MC.Engine.Common;

namespace MC.Engine.Core;

/// <summary>
/// 线程安全的状态机管理器
/// </summary>
public class StateMachine
{
    private EngineState _currentState = EngineState.Idle;
    private readonly object _lock = new();

    public EngineState CurrentState
    {
        get => _currentState;
        private set => _currentState = value;
    }

    private static readonly Dictionary<(EngineState, EngineCommand), EngineState> TransitionRules =
        new()
        {
            { (EngineState.Idle, EngineCommand.Start), EngineState.Running },
            { (EngineState.Running, EngineCommand.Pause), EngineState.Paused },
            { (EngineState.Running, EngineCommand.Stop), EngineState.Stopping },
            { (EngineState.Paused, EngineCommand.Resume), EngineState.Running },
            { (EngineState.Paused, EngineCommand.Stop), EngineState.Stopping },
            { (EngineState.Stopping, EngineCommand.StopComplete), EngineState.Idle },
            { (EngineState.Error, EngineCommand.Reset), EngineState.Idle },
        };

    public event EventHandler<StateChangedEventArgs>? StateChanged;

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
