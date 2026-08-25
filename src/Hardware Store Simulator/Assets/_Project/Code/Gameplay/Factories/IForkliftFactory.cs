using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public interface IForkliftFactory
    {
        GameEntity Create(Pose at, int storeEntityId);
    }
}
