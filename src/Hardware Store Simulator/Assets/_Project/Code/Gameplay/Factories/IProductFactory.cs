using HardwareStore.Gameplay.Components;
using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public interface IProductFactory
    {
        GameEntity CreateInbound(ProductTypeId productType, Pose at, int deliveryEntityId,
            int deliverySlotIndex, int purchaseOrderLineEntityId);
    }
}
