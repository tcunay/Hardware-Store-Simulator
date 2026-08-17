using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public interface ITrolleyMotionService
    {
        bool TryResolveMove(Rigidbody trolleyBody, Collider[] trolleyColliders,
            CharacterController sourceController, Vector3 targetPosition,
            Quaternion targetRotation, out Pose resolvedPose);
        bool TryResolveMove(Rigidbody trolleyBody, Collider[] trolleyColliders,
            Transform sourceTransform, float stepHeight,
            Vector3 targetPosition, Quaternion targetRotation,
            out Pose resolvedPose);
    }
}
