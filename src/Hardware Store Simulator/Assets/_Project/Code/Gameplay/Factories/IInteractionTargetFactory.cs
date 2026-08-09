using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public interface IInteractionTargetFactory
    {
        GameEntity CreateOrderCounter(int storeEntityId);
        GameEntity CreateProcurementTerminal(int storeEntityId, int storageZoneEntityId,
            Pose deliveryPose);
        GameEntity CreateStorageZone();
    }
}
