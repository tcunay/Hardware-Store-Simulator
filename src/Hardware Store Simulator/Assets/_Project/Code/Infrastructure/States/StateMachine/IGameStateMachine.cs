using System;
using HardwareStore.Infrastructure.States.StateInfrastructure;

namespace HardwareStore.Infrastructure.States.StateMachine
{
    public interface IGameStateMachine
    {
        Type ActiveStateType { get; }

        void Enter<TState>() where TState : class, IState;
        void Enter<TState, TPayload>(TPayload payload)
            where TState : class, IPayloadState<TPayload>;
    }
}
