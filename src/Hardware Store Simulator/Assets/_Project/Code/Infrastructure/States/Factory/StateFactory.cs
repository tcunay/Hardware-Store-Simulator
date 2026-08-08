using HardwareStore.Infrastructure.States.StateInfrastructure;
using Zenject;

namespace HardwareStore.Infrastructure.States.Factory
{
    public sealed class StateFactory : IStateFactory
    {
        private readonly DiContainer _container;

        public StateFactory(DiContainer container) => _container = container;

        public TState GetState<TState>() where TState : class, IExitableState =>
            _container.Resolve<TState>();
    }
}
