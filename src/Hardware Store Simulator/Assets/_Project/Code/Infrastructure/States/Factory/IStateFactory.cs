using HardwareStore.Infrastructure.States.StateInfrastructure;

namespace HardwareStore.Infrastructure.States.Factory
{
    public interface IStateFactory
    {
        TState GetState<TState>() where TState : class, IExitableState;
    }
}
