using Entitas;

namespace HardwareStore.Infrastructure.Systems
{
    public interface ISystemFactory
    {
        T Create<T>() where T : ISystem;
        T Create<T>(params object[] arguments) where T : ISystem;
    }
}
