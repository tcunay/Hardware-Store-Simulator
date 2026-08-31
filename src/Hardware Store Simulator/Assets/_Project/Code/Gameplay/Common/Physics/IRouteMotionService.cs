using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public interface IRouteMotionService
    {
        bool TryResolveMove(Rigidbody body, Collider[] colliders,
            Vector3 targetPosition, Quaternion targetRotation,
            out Pose resolvedPose, out Collider blockingCollider);
    }
}
