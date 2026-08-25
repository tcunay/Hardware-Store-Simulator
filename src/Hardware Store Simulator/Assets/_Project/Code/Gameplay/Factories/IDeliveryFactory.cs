using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public interface IDeliveryFactory
    {
        GameEntity Create(int purchaseOrderEntityId, int procurementTerminalEntityId,
            int storeEntityId, Pose at);

    }
}
