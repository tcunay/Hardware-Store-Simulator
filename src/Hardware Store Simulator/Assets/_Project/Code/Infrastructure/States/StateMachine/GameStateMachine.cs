using System;
using HardwareStore.Infrastructure.States.Factory;
using HardwareStore.Infrastructure.States.StateInfrastructure;
using Zenject;

namespace HardwareStore.Infrastructure.States.StateMachine
{
    public sealed class GameStateMachine : IGameStateMachine, ITickable,
        IFixedTickable, IDisposable
    {
        private readonly IStateFactory _stateFactory;
        private IExitableState _activeState;
        private Action _pendingTransition;
        private bool _isExecuting;
        private bool _isDisposed;

        public GameStateMachine(IStateFactory stateFactory) => _stateFactory = stateFactory;

        public Type ActiveStateType => _activeState?.GetType();

        public void Enter<TState>() where TState : class, IState =>
            RequestTransition(() => ChangeState<TState>(state => state.Enter()));

        public void Enter<TState, TPayload>(TPayload payload)
            where TState : class, IPayloadState<TPayload> =>
            RequestTransition(() => ChangeState<TState>(state => state.Enter(payload)));

        public void Tick() =>
            ExecuteActiveState(() =>
            {
                if (_activeState is IUpdateable updateableState)
                    updateableState.Update();
            });

        public void FixedTick() =>
            ExecuteActiveState(() =>
            {
                if (_activeState is IFixedUpdateable fixedUpdateableState)
                    fixedUpdateableState.FixedUpdate();
            });

        public void Dispose()
        {
            if (_isDisposed)
                return;

            if (_isExecuting)
            {
                throw new InvalidOperationException(
                    "The game state machine cannot be disposed during a state tick.");
            }

            _isDisposed = true;
            _pendingTransition = null;
            IExitableState activeState = _activeState;
            _activeState = null;
            activeState?.Exit();
        }

        private void RequestTransition(Action transition)
        {
            ThrowIfDisposed();

            if (!_isExecuting)
            {
                transition();
                return;
            }

            if (_pendingTransition != null)
            {
                throw new InvalidOperationException(
                    "Only one state transition may be requested during a state tick.");
            }

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

        private void ExecuteActiveState(Action execute)
        {
            ThrowIfDisposed();
            if (_isExecuting)
            {
                throw new InvalidOperationException(
                    "The game state machine cannot execute nested state ticks.");
            }

            _isExecuting = true;
            try
            {
                execute();
            }
            catch
            {
                _pendingTransition = null;
                throw;
            }
            finally
            {
                _isExecuting = false;
            }

            Action transition = _pendingTransition;
            _pendingTransition = null;
            transition?.Invoke();
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(GameStateMachine));
        }
    }
}
