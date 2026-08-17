using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public interface IWarehouseWorkerTrolleyFactory
    {
        GameEntity Create(int storeEntityId, Pose homePose,
            Pose customerLoadingPose);
    }
}
