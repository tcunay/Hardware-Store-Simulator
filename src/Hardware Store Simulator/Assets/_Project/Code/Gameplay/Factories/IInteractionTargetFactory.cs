using HardwareStore.Infrastructure.View;

namespace HardwareStore.Gameplay.Factories
{
    public interface IInteractionTargetFactory
    {
        GameEntity CreateOrderCounter(EntityBehaviour view, int orderEntityId);
        GameEntity CreateLoadingZone(EntityBehaviour view, int orderEntityId);
    }
}
