using System;
using HardwareStore.Infrastructure.States.Factory;
using HardwareStore.Infrastructure.States.StateInfrastructure;
using Zenject;

namespace HardwareStore.Infrastructure.States.StateMachine
{
    public sealed class GameStateMachine : IGameStateMachine, ITickable, IDisposable
    {
        private readonly IStateFactory _stateFactory;
        private IExitableState _activeState;
        private Action _pendingTransition;
        private bool _isUpdating;
        private bool _isDisposed;

        public GameStateMachine(IStateFactory stateFactory) => _stateFactory = stateFactory;

        public Type ActiveStateType => _activeState?.GetType();

        public void Enter<TState>() where TState : class, IState =>
            RequestTransition(() => ChangeState<TState>(state => state.Enter()));

        public void Enter<TState, TPayload>(TPayload payload)
            where TState : class, IPayloadState<TPayload> =>
            RequestTransition(() => ChangeState<TState>(state => state.Enter(payload)));

        public void Tick()
        {
            ThrowIfDisposed();

            _isUpdating = true;
            try
            {
                if (_activeState is IUpdateable updateableState)
                    updateableState.Update();
            }
            catch
            {
                _pendingTransition = null;
                throw;
            }
            finally
            {
                _isUpdating = false;
            }

            Action transition = _pendingTransition;
            _pendingTransition = null;
            transition?.Invoke();
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            if (_isUpdating)
                throw new InvalidOperationException("The game state machine cannot be disposed during a state update.");

            _isDisposed = true;
            _pendingTransition = null;
            IExitableState activeState = _activeState;
            _activeState = null;
            activeState?.Exit();
        }

        private void RequestTransition(Action transition)
        {
            ThrowIfDisposed();

            if (!_isUpdating)
            {
                transition();
                return;
            }

            if (_pendingTransition != null)
                throw new InvalidOperationException("Only one state transition may be requested during an update.");

            _pendingTransition = transition;
        }

        private void ChangeState<TState>(Action<TState> enter)
            where TState : class, IExitableState
        {
            TState nextState = _stateFactory.GetState<TState>();
            _activeState?.Exit();
            _activeState = nextState;
            enter(nextState);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(GameStateMachine));
        }
    }
}
