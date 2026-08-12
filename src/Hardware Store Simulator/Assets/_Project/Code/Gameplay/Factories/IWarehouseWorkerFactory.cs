using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public interface IWarehouseWorkerFactory
    {
        GameEntity Create(int storeEntityId, Pose spawnPose, Pose pickupPose,
            Pose storagePose);
    }
}
