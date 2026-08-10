using HardwareStore.Gameplay.Components;
using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public interface IDeliveryFactory
    {
        GameEntity Create(ProductTypeId productType, int procurementTerminalEntityId,
            int storeEntityId, Pose at);
    }
}
