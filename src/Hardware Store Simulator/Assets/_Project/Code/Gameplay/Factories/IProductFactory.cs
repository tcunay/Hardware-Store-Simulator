using HardwareStore.Infrastructure.View;

namespace HardwareStore.Gameplay.Factories
{
    public interface IProductFactory
    {
        GameEntity Create(EntityBehaviour view);
    }
}
