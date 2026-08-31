using System;
using HardwareStore.Gameplay.Common.Time;
using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public sealed class CustomerVehiclePhysicsMotor :
        ICustomerVehiclePhysicsMotor
    {
        private const float InputTolerance = 0.0001f;
        private const float WaypointRotationTolerance = 1f;
        private const float MaximumArrivalSpeed = 0.08f;
        private const float MaximumArrivalAngularSpeed = 2f;
        private const float MinimumRouteAlignment = 0.25f;
        private const float MaximumCrossTrackCorrection = 0.75f;
        private const float AngularAccelerationMultiplier = 4f;
        private const int SolverIterations = 12;
        private const int SolverVelocityIterations = 4;
        private const float MaximumLinearVelocity = 8f;
        private const float MaximumAngularVelocity = 3f;

        private readonly IPhysicsTimeService _time;

        public CustomerVehiclePhysicsMotor(IPhysicsTimeService time) =>
            _time = time;

        public CustomerVehiclePhysicsMotorResult Step(Rigidbody body,
            Collider[] colliders, Pose segmentStart, Pose target,
            float maximumSpeed, float rotationSpeed,
            float waypointTolerance, float acceleration, float braking,
            bool yielding)
        {
            ConfigureBody(body);
            Validate(body, colliders, segmentStart, target, maximumSpeed,
                rotationSpeed, waypointTolerance, acceleration, braking);
            float deltaTime = _time.FixedDeltaTime;
            if (!IsFinite(deltaTime) || deltaTime <= 0f)
                throw new InvalidOperationException(
                    "Customer-vehicle fixed delta time must be finite and positive.");

            Vector3 segment = ResolveSegment(segmentStart, target);
            Vector3 segmentDirection = segment.normalized;
            Vector3 toTarget = Horizontal(target.position - body.position);
            float remainingDistance = Vector3.Dot(toTarget, segmentDirection);
            Vector3 crossTrackCorrection =
                toTarget - segmentDirection * remainingDistance;
            float crossTrackDistance = crossTrackCorrection.magnitude;
            float angle = Mathf.Abs(Mathf.DeltaAngle(
                body.rotation.eulerAngles.y, target.rotation.eulerAngles.y));
            Vector3 currentHorizontalVelocity = Horizontal(body.linearVelocity);
            float currentSpeed = currentHorizontalVelocity.magnitude;
            float currentAngularSpeed = Mathf.Abs(
                body.angularVelocity.y * Mathf.Rad2Deg);
            bool positionReached = remainingDistance <= waypointTolerance &&
                crossTrackDistance <= waypointTolerance;
            bool waypointReached = positionReached &&
                angle <= WaypointRotationTolerance &&
                currentSpeed <= MaximumArrivalSpeed &&
                currentAngularSpeed <= MaximumArrivalAngularSpeed;
            if (waypointReached)
            {
                StopHorizontalMotion(body);
                return new CustomerVehiclePhysicsMotorResult(
                    true, currentSpeed);
            }

            bool reverse = IsReverseSegment(segmentDirection, target);
            float stoppingDistance = Mathf.Max(
                Mathf.Max(
                    remainingDistance - waypointTolerance,
                    crossTrackDistance - waypointTolerance),
                0f);
            float arrivalLimitedSpeed = Mathf.Sqrt(
                2f * braking * stoppingDistance);
            float desiredSpeed = yielding
                ? 0f
                : Mathf.Min(maximumSpeed, arrivalLimitedSpeed);
            float signedSpeed = reverse ? -desiredSpeed : desiredSpeed;
            Vector3 desiredHorizontalVelocity =
                body.rotation * Vector3.forward * signedSpeed;
            float linearRate = desiredSpeed < currentSpeed
                ? braking
                : acceleration;
            Vector3 resolvedHorizontalVelocity = Vector3.MoveTowards(
                currentHorizontalVelocity,
                desiredHorizontalVelocity,
                linearRate * deltaTime);
            body.AddForce(
                resolvedHorizontalVelocity - currentHorizontalVelocity,
                ForceMode.VelocityChange);

            Quaternion desiredRotation = ResolveDesiredRotation(
                segmentDirection, crossTrackCorrection,
                target.rotation, reverse, positionReached);
            float desiredYawSpeed = yielding
                ? 0f
                : Mathf.Clamp(
                    Mathf.DeltaAngle(
                        body.rotation.eulerAngles.y,
                        desiredRotation.eulerAngles.y) / deltaTime,
                    -rotationSpeed,
                    rotationSpeed);
            float currentYawSpeed = body.angularVelocity.y * Mathf.Rad2Deg;
            float resolvedYawSpeed = Mathf.MoveTowards(
                currentYawSpeed,
                desiredYawSpeed,
                rotationSpeed * AngularAccelerationMultiplier * deltaTime);
            body.AddTorque(
                Vector3.up * ((resolvedYawSpeed - currentYawSpeed) *
                              Mathf.Deg2Rad),
                ForceMode.VelocityChange);

            return new CustomerVehiclePhysicsMotorResult(
                false,
                currentSpeed);
        }

        public void Hold(Rigidbody body, float braking)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));
            ConfigureBody(body);
            if (!body.gameObject.activeInHierarchy || body.isKinematic ||
                !body.useGravity || !body.detectCollisions)
            {
                throw new InvalidOperationException(
                    "A parked customer vehicle requires an active collision-enabled " +
                    "dynamic Rigidbody with gravity.");
            }
            if (!IsFinite(braking) || braking <= 0f)
                throw new InvalidOperationException(
                    "Parked customer-vehicle braking must be finite and positive.");

            float deltaTime = _time.FixedDeltaTime;
            if (!IsFinite(deltaTime) || deltaTime <= 0f)
                throw new InvalidOperationException(
                    "Customer-vehicle fixed delta time must be finite and positive.");

            Vector3 horizontalVelocity = Horizontal(body.linearVelocity);
            Vector3 resolvedVelocity = Vector3.MoveTowards(
                horizontalVelocity, Vector3.zero, braking * deltaTime);
            body.AddForce(
                resolvedVelocity - horizontalVelocity,
                ForceMode.VelocityChange);
            float resolvedYawSpeed = Mathf.MoveTowards(
                body.angularVelocity.y, 0f,
                MaximumAngularVelocity * deltaTime);
            body.AddTorque(
                Vector3.up * (resolvedYawSpeed - body.angularVelocity.y),
                ForceMode.VelocityChange);
        }

        private static void ConfigureBody(Rigidbody body)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));
            body.solverIterations = SolverIterations;
            body.solverVelocityIterations = SolverVelocityIterations;
            body.maxLinearVelocity = MaximumLinearVelocity;
            body.maxAngularVelocity = MaximumAngularVelocity;
        }

        private static Vector3 ResolveSegment(Pose segmentStart, Pose target)
        {
            Vector3 segment = Horizontal(
                target.position - segmentStart.position);
            if (segment.sqrMagnitude <= InputTolerance * InputTolerance)
                throw new InvalidOperationException(
                    "Customer-vehicle route segment must have horizontal length.");

            return segment;
        }

        private static bool IsReverseSegment(Vector3 segmentDirection,
            Pose target)
        {
            float alignment = Vector3.Dot(
                target.rotation * Vector3.forward,
                segmentDirection);
            if (Mathf.Abs(alignment) < MinimumRouteAlignment)
            {
                throw new InvalidOperationException(
                    "Customer-vehicle route target rotation must identify forward or " +
                    "reverse travel along its segment.");
            }

            return alignment < 0f;
        }

        private static Quaternion ResolveDesiredRotation(
            Vector3 segmentDirection, Vector3 crossTrackCorrection,
            Quaternion targetRotation, bool reverse,
            bool positionReached)
        {
            if (positionReached)
                return targetRotation;

            Vector3 correction = Vector3.ClampMagnitude(
                crossTrackCorrection, MaximumCrossTrackCorrection);
            Vector3 travelDirection =
                (segmentDirection + correction).normalized;
            Vector3 forward = reverse ? -travelDirection : travelDirection;
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private static void StopHorizontalMotion(Rigidbody body)
        {
            Vector3 velocity = body.linearVelocity;
            body.AddForce(
                -Horizontal(velocity),
                ForceMode.VelocityChange);
            body.AddTorque(
                Vector3.down * body.angularVelocity.y,
                ForceMode.VelocityChange);
        }

        private static void Validate(Rigidbody body, Collider[] colliders,
            Pose segmentStart, Pose target, float maximumSpeed,
            float rotationSpeed, float waypointTolerance,
            float acceleration, float braking)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));
            if (colliders == null)
                throw new ArgumentNullException(nameof(colliders));
            if (!body.gameObject.activeInHierarchy || body.isKinematic ||
                !body.useGravity || !body.detectCollisions)
            {
                throw new InvalidOperationException(
                    "Customer-vehicle motor requires an active collision-enabled " +
                    "dynamic Rigidbody with gravity.");
            }
            if ((body.constraints & RigidbodyConstraints.FreezeRotationX) == 0 ||
                (body.constraints & RigidbodyConstraints.FreezeRotationZ) == 0)
            {
                throw new InvalidOperationException(
                    "Customer-vehicle Rigidbody must freeze pitch and roll.");
            }
            if (body.interpolation == RigidbodyInterpolation.None)
                throw new InvalidOperationException(
                    "Customer-vehicle Rigidbody must use interpolation.");
            if (!IsFinite(segmentStart.position) ||
                !IsFinite(target.position) ||
                !IsNormalized(segmentStart.rotation) ||
                !IsNormalized(target.rotation))
            {
                throw new InvalidOperationException(
                    "Customer-vehicle route poses must be finite and normalized.");
            }
            if (!IsFinite(maximumSpeed) || maximumSpeed <= 0f ||
                !IsFinite(rotationSpeed) || rotationSpeed <= 0f ||
                !IsFinite(waypointTolerance) || waypointTolerance <= 0f ||
                !IsFinite(acceleration) || acceleration <= 0f ||
                !IsFinite(braking) || braking <= 0f)
            {
                throw new InvalidOperationException(
                    "Customer-vehicle motor values must be finite and positive.");
            }

            int solidColliderCount = 0;
            foreach (Collider collider in colliders)
            {
                if (collider == null)
                    throw new InvalidOperationException(
                        "Customer-vehicle Colliders contains a missing reference.");
                if (collider.attachedRigidbody != body ||
                    !collider.transform.IsChildOf(body.transform))
                {
                    throw new InvalidOperationException(
                        "Customer-vehicle collider must belong to its Rigidbody root.");
                }
                if (!collider.enabled || !collider.gameObject.activeInHierarchy ||
                    collider.isTrigger)
                {
                    continue;
                }

                solidColliderCount++;
            }
            if (solidColliderCount != 1)
                throw new InvalidOperationException(
                    "Customer-vehicle motor requires exactly one active solid collider.");
        }

        private static Vector3 Horizontal(Vector3 value) =>
            new(value.x, 0f, value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsNormalized(Quaternion value)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) ||
                !IsFinite(value.z) || !IsFinite(value.w))
            {
                return false;
            }

            float length = Mathf.Sqrt(
                value.x * value.x + value.y * value.y +
                value.z * value.z + value.w * value.w);
            return length >= 0.999f && length <= 1.001f;
        }
    }
}
