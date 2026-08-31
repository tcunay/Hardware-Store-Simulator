using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public interface IWorkerTrolleyHitchService
    {
        void BeginFrame();

        void Maintain(Rigidbody trolleyBody, Collider[] trolleyColliders,
            Rigidbody workerBody, Collider[] workerColliders,
            float followDistance);

        void EndFrame();

        void Detach(Rigidbody trolleyBody);

        void DetachAll();
    }
}
