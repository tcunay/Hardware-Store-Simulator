using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public interface ITrolleyMotionService
    {
        bool TryResolveMove(Rigidbody trolleyBody, Collider[] trolleyColliders,
            CharacterController sourceController, Vector3 targetPosition,
            Quaternion targetRotation, out Pose resolvedPose);
    }
}
