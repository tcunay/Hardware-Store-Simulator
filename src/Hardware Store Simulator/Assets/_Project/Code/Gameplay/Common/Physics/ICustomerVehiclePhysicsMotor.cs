using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public readonly struct CustomerVehiclePhysicsMotorResult
    {
        public CustomerVehiclePhysicsMotorResult(bool waypointReached,
            float observedSpeed)
        {
            WaypointReached = waypointReached;
            ObservedSpeed = observedSpeed;
        }

        public bool WaypointReached { get; }
        public float ObservedSpeed { get; }
    }

    public interface ICustomerVehiclePhysicsMotor
    {
        CustomerVehiclePhysicsMotorResult Step(Rigidbody body,
            Collider[] colliders, Pose segmentStart, Pose target,
            float maximumSpeed, float rotationSpeed,
            float waypointTolerance, float acceleration, float braking,
            bool yielding);

        void Hold(Rigidbody body, float braking);
    }
}
