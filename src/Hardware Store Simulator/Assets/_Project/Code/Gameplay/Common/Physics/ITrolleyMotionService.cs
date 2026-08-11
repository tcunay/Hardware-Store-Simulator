using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public interface ITrolleyMotionService
    {
        bool CanMoveTo(Rigidbody trolleyBody, Collider[] trolleyColliders,
            CharacterController sourceController, Vector3 targetPosition,
            Quaternion targetRotation);
    }
}
