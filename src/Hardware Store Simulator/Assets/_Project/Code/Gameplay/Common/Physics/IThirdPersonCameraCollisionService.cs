using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public interface IThirdPersonCameraCollisionService
    {
        Vector3 ResolvePosition(
            Vector3 pivotPosition,
            Vector3 desiredPosition,
            float collisionRadius,
            float collisionPadding,
            Transform driverRoot,
            Transform vehicleRoot);
    }
}
