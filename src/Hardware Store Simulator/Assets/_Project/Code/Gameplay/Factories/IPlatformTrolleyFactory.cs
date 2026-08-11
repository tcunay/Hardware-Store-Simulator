using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public interface IPlatformTrolleyFactory
    {
        GameEntity Create(Pose at, int storeEntityId);
    }
}
