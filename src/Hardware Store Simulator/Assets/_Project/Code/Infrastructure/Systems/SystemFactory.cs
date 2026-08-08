using Entitas;
using Zenject;

namespace HardwareStore.Infrastructure.Systems
{
    public sealed class SystemFactory : ISystemFactory
    {
        private readonly DiContainer _container;

        public SystemFactory(DiContainer container) => _container = container;

        public T Create<T>() where T : ISystem => _container.Instantiate<T>();

        public T Create<T>(params object[] arguments) where T : ISystem =>
            _container.Instantiate<T>(arguments);
    }
}
