using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public interface IProductDropPhysicsService
    {
        bool TryGetSafeDropPosition(
            Vector3 origin,
            Vector3 forward,
            float forwardDistance,
            float collisionRadius,
            CharacterController sourceController,
            out Vector3 position);
    }
}
