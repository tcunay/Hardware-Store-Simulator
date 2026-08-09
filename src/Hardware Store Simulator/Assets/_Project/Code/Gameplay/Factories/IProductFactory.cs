using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public interface IProductFactory
    {
        GameEntity CreateInbound(Pose at, int deliveryEntityId, int deliverySlotIndex);
    }
}
