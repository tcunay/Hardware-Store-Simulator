using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public interface IDeliveryFactory
    {
        GameEntity Create(int procurementTerminalEntityId, int storeEntityId, Pose at);
    }
}
