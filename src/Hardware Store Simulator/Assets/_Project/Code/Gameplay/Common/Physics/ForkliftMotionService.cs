using System;
using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public sealed class ForkliftMotionService : IForkliftMotionService
    {
        private const int MaxQueryHits = 64;
        private const float InputTolerance = 0.0001f;
        private const float MinimumQueryRadius = 0.001f;
        private const float CollisionTolerance = 0.000001f;

        private readonly ITrolleyMotionService _collisionMotion;
        private readonly Collider[] _singleCollider = new Collider[1];
        private readonly RaycastHit[] _liftSweepHits =
            new RaycastHit[MaxQueryHits];
        private readonly Collider[] _exitOverlapHits =
            new Collider[MaxQueryHits];

        public ForkliftMotionService(ITrolleyMotionService collisionMotion) =>
            _collisionMotion = collisionMotion;

        public void EnterDriver(CharacterController driverController,
            Transform driverTransform, Transform seatAnchor)
        {
            ValidateDriver(driverController, driverTransform, seatAnchor);
            if (!driverController.enabled)
            {
                throw new InvalidOperationException(
                    "Entering a forklift requires an enabled CharacterController.");
            }

            driverController.enabled = false;
            SetPose(driverTransform, seatAnchor);
        }

        public void KeepDriverSeated(CharacterController driverController,
            Transform driverTransform, Transform seatAnchor)
        {
            ValidateDriver(driverController, driverTransform, seatAnchor);
            if (driverController.enabled)
            {
                throw new InvalidOperationException(
                    "A seated forklift driver cannot have an enabled CharacterController.");
            }

            SetPose(driverTransform, seatAnchor);
        }

        public bool TryExitDriver(CharacterController driverController,
            Transform driverTransform, Transform exitAnchor,
            Transform forkliftTransform)
        {
            ValidateDriver(driverController, driverTransform, exitAnchor);
            if (forkliftTransform == null)
                throw new ArgumentNullException(nameof(forkliftTransform));
            if (!forkliftTransform.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException(
                    "Forklift exit requires an active forklift transform.");
            }
            if (driverController.enabled)
            {
                throw new InvalidOperationException(
                    "Exiting a forklift requires a disabled CharacterController.");
            }

            if (!IsDriverExitClear(
                    driverController,
                    driverTransform,
                    exitAnchor,
                    forkliftTransform))
            {
                return false;
            }

            SetPose(driverTransform, exitAnchor);
            driverController.enabled = true;
            return true;
        }

        public void RestoreDriverController(
            CharacterController driverController)
        {
            if (driverController == null)
                throw new ArgumentNullException(nameof(driverController));
            if (!driverController.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException(
                    "Restoring a forklift driver requires an active CharacterController.");
            }

            driverController.enabled = true;
        }

        public bool TryDrive(Rigidbody forkliftBody,
            Collider[] forkliftColliders, float throttle, float steering,
            float forwardSpeed, float reverseSpeed, float steeringSpeed,
            float deltaTime)
        {
            ValidateDriveConfiguration(
                forkliftBody,
                forkliftColliders,
                forwardSpeed,
                reverseSpeed,
                steeringSpeed,
                deltaTime);

            float clampedThrottle = Mathf.Clamp(throttle, -1f, 1f);
            float clampedSteering = Mathf.Clamp(steering, -1f, 1f);
            if (deltaTime == 0f ||
                Mathf.Abs(clampedThrottle) <= InputTolerance)
            {
                return false;
            }

            float travelSpeed = clampedThrottle >= 0f
                ? forwardSpeed
                : reverseSpeed;
            float reverseSteeringSign = clampedThrottle >= 0f ? 1f : -1f;
            float yaw = clampedSteering * steeringSpeed * deltaTime *
                        Mathf.Abs(clampedThrottle) * reverseSteeringSign;
            Quaternion targetRotation = Quaternion.AngleAxis(yaw, Vector3.up) *
                                        forkliftBody.rotation;
            Vector3 targetPosition = forkliftBody.position +
                                     targetRotation * Vector3.forward *
                                     (clampedThrottle * travelSpeed * deltaTime);

            int collisionHullCount = 0;
            foreach (Collider forkliftCollider in forkliftColliders)
            {
                if (forkliftCollider == null)
                {
                    throw new InvalidOperationException(
                        "Forklift Colliders contains a missing reference.");
                }
                if (!forkliftCollider.enabled || forkliftCollider.isTrigger)
                    continue;

                collisionHullCount++;
                _singleCollider[0] = forkliftCollider;
                if (!_collisionMotion.TryResolveMove(
                        forkliftBody,
                        _singleCollider,
                        forkliftBody.transform,
                        0f,
                        targetPosition,
                        targetRotation,
                        out Pose ignored))
                {
                    return false;
                }
            }
            if (collisionHullCount == 0)
            {
                throw new InvalidOperationException(
                    "Forklift requires at least one enabled solid collision hull.");
            }

            forkliftBody.position = targetPosition;
            forkliftBody.rotation = targetRotation;
            forkliftBody.transform.SetPositionAndRotation(
                targetPosition,
                targetRotation);
            return true;
        }

        public bool TrySetForkHeight(Transform forkliftTransform,
            Transform liftTransform, Collider[] forkliftColliders,
            float height)
        {
            if (forkliftTransform == null)
                throw new ArgumentNullException(nameof(forkliftTransform));
            if (liftTransform == null)
                throw new ArgumentNullException(nameof(liftTransform));
            if (forkliftColliders == null)
                throw new ArgumentNullException(nameof(forkliftColliders));
            if (!IsFinite(height))
                throw new InvalidOperationException("Fork height must be finite.");
            if (!liftTransform.IsChildOf(forkliftTransform) ||
                liftTransform.parent == null)
            {
                throw new InvalidOperationException(
                    "Forklift lift must be a child of the forklift view.");
            }

            Vector3 localPosition = liftTransform.localPosition;
            Vector3 displacement = liftTransform.parent.TransformVector(
                Vector3.up * (height - localPosition.y));
            float distance = displacement.magnitude;
            if (distance <= InputTolerance)
            {
                localPosition.y = height;
                liftTransform.localPosition = localPosition;
                return true;
            }

            UnityEngine.Physics.SyncTransforms();
            Vector3 direction = displacement / distance;
            int liftHullCount = 0;
            foreach (Collider forkliftCollider in forkliftColliders)
            {
                if (forkliftCollider == null)
                {
                    throw new InvalidOperationException(
                        "Forklift Colliders contains a missing reference.");
                }
                if (!forkliftCollider.enabled || forkliftCollider.isTrigger ||
                    !forkliftCollider.transform.IsChildOf(liftTransform))
                {
                    continue;
                }
                if (forkliftCollider is not BoxCollider liftHull)
                {
                    throw new InvalidOperationException(
                        "Forklift lift collision hulls must be BoxColliders.");
                }

                liftHullCount++;
                if (HasBlockingLiftSweep(
                        liftHull,
                        forkliftTransform,
                        displacement,
                        direction,
                        distance))
                {
                    return false;
                }
            }
            if (liftHullCount == 0)
            {
                throw new InvalidOperationException(
                    "Forklift requires an enabled solid lift collision hull.");
            }

            localPosition.y = height;
            liftTransform.localPosition = localPosition;
            return true;
        }

        public void PlacePallet(Transform palletTransform, Transform anchor)
        {
            if (palletTransform == null)
                throw new ArgumentNullException(nameof(palletTransform));
            if (anchor == null)
                throw new ArgumentNullException(nameof(anchor));
            if (!palletTransform.gameObject.activeInHierarchy ||
                !anchor.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException(
                    "Pallet placement requires active transforms.");
            }

            SetPose(palletTransform, anchor);
        }

        private static void ValidateDriver(CharacterController controller,
            Transform driverTransform, Transform anchor)
        {
            if (controller == null)
                throw new ArgumentNullException(nameof(controller));
            if (driverTransform == null)
                throw new ArgumentNullException(nameof(driverTransform));
            if (anchor == null)
                throw new ArgumentNullException(nameof(anchor));
            if (!ReferenceEquals(controller.transform, driverTransform))
            {
                throw new InvalidOperationException(
                    "The driver CharacterController must be on the driver root.");
            }
            if (!driverTransform.gameObject.activeInHierarchy ||
                !anchor.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException(
                    "Forklift driver motion requires active transforms.");
            }
        }

        private bool IsDriverExitClear(CharacterController controller,
            Transform driverTransform, Transform exitAnchor,
            Transform forkliftTransform)
        {
            Vector3 scale = driverTransform.lossyScale;
            float horizontalScale = Mathf.Max(
                Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float verticalScale = Mathf.Abs(scale.y);
            if (!IsFinite(horizontalScale) || horizontalScale <= 0f ||
                !IsFinite(verticalScale) || verticalScale <= 0f)
            {
                throw new InvalidOperationException(
                    "Forklift driver scale must be finite and positive.");
            }

            float radius = controller.radius * horizontalScale;
            float height = controller.height * verticalScale;
            float queryRadius = Mathf.Max(
                radius - controller.skinWidth * horizontalScale,
                MinimumQueryRadius);
            float halfSegment = Mathf.Max(height * 0.5f - radius, 0f);
            Vector3 center = exitAnchor.position +
                             exitAnchor.rotation *
                             Vector3.Scale(controller.center, scale);
            Vector3 axisOffset = exitAnchor.rotation * Vector3.up * halfSegment;

            UnityEngine.Physics.SyncTransforms();
            int hitCount = UnityEngine.Physics.OverlapCapsuleNonAlloc(
                center - axisOffset,
                center + axisOffset,
                queryRadius,
                _exitOverlapHits,
                UnityEngine.Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            if (hitCount >= _exitOverlapHits.Length)
            {
                throw new InvalidOperationException(
                    "Forklift driver exit overlap buffer was saturated.");
            }

            int driverLayer = driverTransform.gameObject.layer;
            for (int index = 0; index < hitCount; index++)
            {
                Collider hit = _exitOverlapHits[index];
                if (hit == null)
                {
                    throw new InvalidOperationException(
                        "Forklift driver exit overlap returned a missing collider.");
                }
                if (ReferenceEquals(hit, controller) ||
                    hit.transform.IsChildOf(driverTransform) ||
                    hit.transform.IsChildOf(forkliftTransform) ||
                    UnityEngine.Physics.GetIgnoreLayerCollision(
                        driverLayer, hit.gameObject.layer) ||
                    UnityEngine.Physics.GetIgnoreCollision(controller, hit))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private bool HasBlockingLiftSweep(BoxCollider liftHull,
            Transform forkliftTransform, Vector3 displacement,
            Vector3 direction, float distance)
        {
            Vector3 halfExtents = Vector3.Scale(
                liftHull.size * 0.5f,
                PositiveScale(liftHull.transform.lossyScale));
            Vector3 center = liftHull.transform.TransformPoint(liftHull.center);
            Quaternion rotation = liftHull.transform.rotation;
            int hitCount = UnityEngine.Physics.BoxCastNonAlloc(
                center,
                halfExtents,
                direction,
                _liftSweepHits,
                rotation,
                distance,
                UnityEngine.Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            if (hitCount >= _liftSweepHits.Length)
            {
                throw new InvalidOperationException(
                    "Forklift lift sweep buffer was saturated.");
            }

            for (int index = 0; index < hitCount; index++)
            {
                Collider candidate = _liftSweepHits[index].collider;
                if (candidate == null)
                {
                    throw new InvalidOperationException(
                        "Forklift lift sweep returned a missing collider.");
                }
                if (ShouldIgnoreCollision(
                        liftHull, candidate, forkliftTransform))
                {
                    continue;
                }

                float hitDistance = _liftSweepHits[index].distance;
                if (!IsFinite(hitDistance) || hitDistance < 0f ||
                    hitDistance > distance + InputTolerance)
                {
                    throw new InvalidOperationException(
                        "Forklift lift sweep returned an invalid hit distance.");
                }
                if (hitDistance > InputTolerance ||
                    PenetrationDepthAtTarget(
                        liftHull, candidate, displacement) >
                    CollisionTolerance)
                {
                    return true;
                }
            }

            int overlapCount = UnityEngine.Physics.OverlapBoxNonAlloc(
                center + displacement,
                halfExtents,
                _exitOverlapHits,
                rotation,
                UnityEngine.Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            if (overlapCount >= _exitOverlapHits.Length)
            {
                throw new InvalidOperationException(
                    "Forklift lift target overlap buffer was saturated.");
            }
            for (int index = 0; index < overlapCount; index++)
            {
                Collider candidate = _exitOverlapHits[index];
                if (candidate == null)
                {
                    throw new InvalidOperationException(
                        "Forklift lift target overlap returned a missing collider.");
                }
                if (!ShouldIgnoreCollision(
                        liftHull, candidate, forkliftTransform) &&
                    PenetrationDepthAtTarget(
                        liftHull, candidate, displacement) >
                    CollisionTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldIgnoreCollision(Collider source,
            Collider candidate, Transform forkliftTransform) =>
            candidate.transform.IsChildOf(forkliftTransform) ||
            UnityEngine.Physics.GetIgnoreLayerCollision(
                source.gameObject.layer, candidate.gameObject.layer) ||
            UnityEngine.Physics.GetIgnoreCollision(source, candidate);

        private static float PenetrationDepthAtTarget(BoxCollider liftHull,
            Collider candidate, Vector3 displacement)
        {
            if (!UnityEngine.Physics.ComputePenetration(
                    liftHull,
                    liftHull.transform.position + displacement,
                    liftHull.transform.rotation,
                    candidate,
                    candidate.transform.position,
                    candidate.transform.rotation,
                    out Vector3 direction,
                    out float distance))
            {
                return 0f;
            }
            if (!IsFinite(direction) || !IsFinite(distance) || distance < 0f)
            {
                throw new InvalidOperationException(
                    "Forklift lift penetration query returned invalid data.");
            }

            return distance;
        }

        private static void ValidateDriveConfiguration(Rigidbody body,
            Collider[] colliders, float forwardSpeed, float reverseSpeed,
            float steeringSpeed, float deltaTime)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));
            if (colliders == null)
                throw new ArgumentNullException(nameof(colliders));
            if (!IsFinite(forwardSpeed) || forwardSpeed <= 0f ||
                !IsFinite(reverseSpeed) || reverseSpeed <= 0f ||
                !IsFinite(steeringSpeed) || steeringSpeed <= 0f ||
                !IsFinite(deltaTime) || deltaTime < 0f)
            {
                throw new InvalidOperationException(
                    "Forklift motion values must be finite and positive; delta time may be zero.");
            }
        }

        private static void SetPose(Transform target, Transform anchor) =>
            target.SetPositionAndRotation(anchor.position, anchor.rotation);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static Vector3 PositiveScale(Vector3 scale)
        {
            if (!IsFinite(scale) || scale.x <= 0f ||
                scale.y <= 0f || scale.z <= 0f)
            {
                throw new InvalidOperationException(
                    "Forklift collision hull scale must be finite and positive.");
            }

            return scale;
        }
    }
}
