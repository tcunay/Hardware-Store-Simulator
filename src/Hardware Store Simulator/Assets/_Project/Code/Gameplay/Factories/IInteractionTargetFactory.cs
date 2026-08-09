using UnityEngine;
using HardwareStore.Infrastructure.View;

namespace HardwareStore.Gameplay.Factories
{
    public interface IInteractionTargetFactory
    {
        GameEntity CreateOrderCounter(EntityBehaviour view, int orderEntityId);
        GameEntity CreateCustomerLoadingZone(EntityBehaviour view, int orderEntityId);
        GameEntity CreateProcurementTerminal(EntityBehaviour view, int storeEntityId,
            int storageZoneEntityId, Pose deliveryPose);
        GameEntity CreateStorageZone(EntityBehaviour view);
    }
}
