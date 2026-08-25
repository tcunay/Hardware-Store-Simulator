using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public interface IForkliftMotionService
    {
        void EnterDriver(CharacterController driverController,
            Transform driverTransform, Transform seatAnchor);
        void KeepDriverSeated(CharacterController driverController,
            Transform driverTransform, Transform seatAnchor);
        bool TryExitDriver(CharacterController driverController,
            Transform driverTransform, Transform exitAnchor,
            Transform forkliftTransform);
        void RestoreDriverController(CharacterController driverController);
        bool TryDrive(Rigidbody forkliftBody, Collider[] forkliftColliders,
            float throttle, float steering, float forwardSpeed,
            float reverseSpeed, float steeringSpeed, float deltaTime);
        bool TrySetForkHeight(Transform forkliftTransform,
            Transform liftTransform, Collider[] forkliftColliders,
            float height);
        void PlacePallet(Transform palletTransform, Transform anchor);
    }
}
