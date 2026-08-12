using System;
using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public sealed class TrolleyMotionService : ITrolleyMotionService
    {
        private const int MaxQueryHits = 64;
        private const float PoseTolerance = 0.001f;
        private const float MinimumSweepDistance = 0.0001f;
        private const float PenetrationTolerance = 0.000001f;
        private const float MaximumRotationStep = 5f;
        private const float MinimumQuaternionLength = 0.999f;
        private const float MaximumQuaternionLength = 1.001f;
        private const float MinimumUprightDot = 0.999f;

        private readonly RaycastHit[] _sweepHits = new RaycastHit[MaxQueryHits];
        private readonly Collider[] _overlapHits = new Collider[MaxQueryHits];

        public bool TryResolveMove(Rigidbody trolleyBody,
            Collider[] trolleyColliders, CharacterController sourceController,
            Vector3 targetPosition, Quaternion targetRotation,
            out Pose resolvedPose)
        {
            BoxCollider hull = ValidateAndGetHull(
                trolleyBody, trolleyColliders, sourceController);
            ValidatePose(targetPosition, targetRotation, "target trolley");
            ValidateStepOffset(sourceController);

            Transform trolleyTransform = trolleyBody.transform;
            ValidatePose(trolleyTransform.position, trolleyTransform.rotation,
                "current trolley");
            if ((trolleyBody.position - trolleyTransform.position).sqrMagnitude >
                PoseTolerance * PoseTolerance ||
                Quaternion.Angle(trolleyBody.rotation, trolleyTransform.rotation) >
                PoseTolerance)
            {
                throw new InvalidOperationException(
                    "Trolley Rigidbody and Transform poses are out of sync.");
            }

            Pose currentPose = new(trolleyBody.position, trolleyBody.rotation);
            resolvedPose = currentPose;
            HullGeometry geometry = CreateHullGeometry(
                trolleyTransform, hull, currentPose);
            Pose targetPose = new(targetPosition, targetRotation);

            if (IsPathClear(
                    hull,
                    geometry,
                    currentPose,
                    targetPose,
                    trolleyTransform,
                    sourceController))
            {
                resolvedPose = targetPose;
                return true;
            }

            float rise = targetPosition.y - currentPose.position.y;
            float stepHeight = sourceController.stepOffset;
            if (Mathf.Abs(rise) > stepHeight + PoseTolerance)
                return false;

            // Use the pusher's authored step limit, but sweep every leg so the
            // fallback can clear a curb without crossing a wall or ceiling.
            Pose raisedPose = new(
                currentPose.position + Vector3.up * stepHeight,
                currentPose.rotation);
            Pose raisedTargetPose = new(
                new Vector3(
                    targetPosition.x,
                    Mathf.Max(raisedPose.position.y, targetPosition.y),
                    targetPosition.z),
                targetRotation);

            if (!IsPathClear(
                    hull,
                    geometry,
                    currentPose,
                    raisedPose,
                    trolleyTransform,
                    sourceController) ||
                !IsPathClear(
                    hull,
                    geometry,
                    raisedPose,
                    raisedTargetPose,
                    trolleyTransform,
                    sourceController) ||
                !IsPathClear(
                    hull,
                    geometry,
                    raisedTargetPose,
                    targetPose,
                    trolleyTransform,
                    sourceController))
            {
                return false;
            }

            resolvedPose = targetPose;
            return true;
        }

        private bool IsPathClear(BoxCollider hull, HullGeometry geometry,
            Pose startPose, Pose targetPose, Transform trolleyTransform,
            CharacterController sourceController)
        {
            HullPose startHullPose = geometry.Resolve(startPose);
            HullPose targetHullPose = geometry.Resolve(targetPose);
            ValidateHullPose(startHullPose, "start trolley hull");
            ValidateHullPose(targetHullPose, "target trolley hull");
            Vector3 displacement = targetHullPose.Center - startHullPose.Center;
            float distance = displacement.magnitude;

            if (distance > MinimumSweepDistance &&
                HasBlockingSweep(
                    hull,
                    startHullPose,
                    geometry.HalfExtents,
                    displacement / distance,
                    distance,
                    trolleyTransform,
                    sourceController))
            {
                return false;
            }

            if (HasBlockingRotationPath(
                    hull,
                    startHullPose,
                    targetHullPose,
                    geometry.HalfExtents,
                    trolleyTransform,
                    sourceController))
            {
                return false;
            }

            return !HasBlockingOverlap(
                hull,
                targetHullPose,
                geometry.HalfExtents,
                trolleyTransform,
                sourceController,
                "target overlap");
        }

        private bool HasBlockingSweep(BoxCollider hull, HullPose origin,
            Vector3 halfExtents, Vector3 direction, float distance,
            Transform trolleyTransform, CharacterController sourceController)
        {
            int hitCount = UnityEngine.Physics.BoxCastNonAlloc(
                origin.Center,
                halfExtents,
                direction,
                _sweepHits,
                origin.Rotation,
                distance,
                UnityEngine.Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            EnsureBufferWasNotSaturated(hitCount, _sweepHits.Length,
                "trolley box cast");
            float contactProbeDistance = ContactProbeDistance();

            for (int index = 0; index < hitCount; index++)
            {
                Collider hit = _sweepHits[index].collider;
                if (hit == null)
                {
                    throw new InvalidOperationException(
                        "Trolley box cast returned a missing collider.");
                }
                if (ShouldIgnore(hit, trolleyTransform, sourceController))
                    continue;

                float hitDistance = _sweepHits[index].distance;
                if (!IsFinite(hitDistance) || hitDistance < 0f ||
                    hitDistance > distance + PoseTolerance)
                {
                    throw new InvalidOperationException(
                        "Trolley box cast returned an invalid hit distance.");
                }
                if (hitDistance > PoseTolerance)
                    return true;

                float probeDistance = Mathf.Min(
                    hitDistance + contactProbeDistance,
                    distance);
                HullPose probePose = origin.Translated(
                    direction * probeDistance);
                if (PenetrationDepth(hull, probePose, hit) >
                    PenetrationTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private static float ContactProbeDistance()
        {
            float contactOffset = UnityEngine.Physics.defaultContactOffset;
            if (!IsFinite(contactOffset) || contactOffset <= 0f)
            {
                throw new InvalidOperationException(
                    "Physics default contact offset must be finite and positive.");
            }

            return contactOffset + PoseTolerance;
        }

        private bool HasBlockingRotationPath(BoxCollider hull,
            HullPose currentPose, HullPose targetPose, Vector3 halfExtents,
            Transform trolleyTransform, CharacterController sourceController)
        {
            float rotationAngle = Quaternion.Angle(
                currentPose.Rotation, targetPose.Rotation);
            int stepCount = Mathf.CeilToInt(rotationAngle / MaximumRotationStep);
            for (int step = 1; step < stepCount; step++)
            {
                float progress = (float)step / stepCount;
                if (HasBlockingOverlap(
                        hull,
                        HullPose.Lerp(currentPose, targetPose, progress),
                        halfExtents,
                        trolleyTransform,
                        sourceController,
                        "rotation-path overlap"))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasBlockingOverlap(BoxCollider hull, HullPose pose,
            Vector3 halfExtents, Transform trolleyTransform,
            CharacterController sourceController, string operation)
        {
            int hitCount = UnityEngine.Physics.OverlapBoxNonAlloc(
                pose.Center,
                halfExtents,
                _overlapHits,
                pose.Rotation,
                UnityEngine.Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            EnsureBufferWasNotSaturated(hitCount, _overlapHits.Length, operation);

            for (int index = 0; index < hitCount; index++)
            {
                Collider hit = _overlapHits[index];
                if (hit == null)
                {
                    throw new InvalidOperationException(
                        $"Trolley {operation} returned a missing collider.");
                }
                if (!ShouldIgnore(hit, trolleyTransform, sourceController) &&
                    PenetrationDepth(hull, pose, hit) > PenetrationTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private static float PenetrationDepth(BoxCollider hull,
            HullPose pose, Collider candidate)
        {
            if (!UnityEngine.Physics.ComputePenetration(
                    hull,
                    pose.TransformPosition,
                    pose.Rotation,
                    candidate,
                    candidate.transform.position,
                    candidate.transform.rotation,
                    out Vector3 separationDirection,
                    out float separationDistance))
            {
                return 0f;
            }

            ValidateVector(separationDirection, "trolley separation direction");
            if (!IsFinite(separationDistance) || separationDistance < 0f)
            {
                throw new InvalidOperationException(
                    "Trolley penetration distance must be finite and non-negative.");
            }

            return separationDistance;
        }

        private static HullGeometry CreateHullGeometry(Transform trolleyTransform,
            BoxCollider hull, Pose currentPose)
        {
            Vector3 currentHullCenter = hull.transform.TransformPoint(hull.center);
            Vector3 hullCenterInTrolleySpace =
                trolleyTransform.InverseTransformPoint(currentHullCenter);
            Vector3 hullTransformPositionInTrolleySpace =
                trolleyTransform.InverseTransformPoint(hull.transform.position);
            Quaternion hullRotationInTrolleySpace =
                Quaternion.Inverse(currentPose.rotation) * hull.transform.rotation;
            Vector3 halfExtents = Vector3.Scale(
                hull.size * 0.5f,
                PositiveScale(hull.transform.lossyScale));

            ValidateVector(currentHullCenter, "current trolley hull center");
            ValidateVector(
                hullCenterInTrolleySpace,
                "trolley-local hull center");
            ValidateVector(
                hullTransformPositionInTrolleySpace,
                "trolley-local hull transform position");
            ValidateVector(halfExtents, "trolley hull half extents");
            ValidateQuaternion(
                hullRotationInTrolleySpace,
                "trolley-local hull rotation");

            return new HullGeometry(
                hullCenterInTrolleySpace,
                hullTransformPositionInTrolleySpace,
                hullRotationInTrolleySpace,
                halfExtents);
        }

        private static BoxCollider ValidateAndGetHull(Rigidbody trolleyBody,
            Collider[] trolleyColliders, CharacterController sourceController)
        {
            if (trolleyBody == null)
                throw new ArgumentNullException(nameof(trolleyBody));
            if (trolleyColliders == null)
                throw new ArgumentNullException(nameof(trolleyColliders));
            if (sourceController == null)
                throw new ArgumentNullException(nameof(sourceController));
            if (!trolleyBody.gameObject.activeInHierarchy || !trolleyBody.isKinematic ||
                trolleyBody.useGravity || !trolleyBody.detectCollisions)
            {
                throw new InvalidOperationException(
                    "Trolley motion requires an active collision-enabled kinematic Rigidbody.");
            }
            if (!sourceController.enabled || !sourceController.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException(
                    "Trolley motion requires an active source CharacterController.");
            }

            Vector3 rootScale = trolleyBody.transform.lossyScale;
            ValidateVector(rootScale, "trolley root scale");
            if (!ApproximatelyOne(rootScale.x) || !ApproximatelyOne(rootScale.y) ||
                !ApproximatelyOne(rootScale.z))
            {
                throw new InvalidOperationException(
                    "Trolley motion requires a unit world scale on the Rigidbody root.");
            }

            BoxCollider hull = null;
            int enabledSolidColliderCount = 0;
            foreach (Collider trolleyCollider in trolleyColliders)
            {
                if (trolleyCollider == null)
                {
                    throw new InvalidOperationException(
                        "Trolley Colliders contains a missing reference.");
                }
                if (trolleyCollider.attachedRigidbody != trolleyBody ||
                    !IsInHierarchy(trolleyCollider.transform, trolleyBody.transform))
                {
                    throw new InvalidOperationException(
                        $"Trolley collider {trolleyCollider.name} is not attached to its Rigidbody.");
                }
                if (!trolleyCollider.enabled || trolleyCollider.isTrigger)
                    continue;

                enabledSolidColliderCount++;
                if (trolleyCollider is not BoxCollider boxCollider)
                {
                    throw new InvalidOperationException(
                        "The enabled trolley collision hull must be a BoxCollider.");
                }

                hull = boxCollider;
            }

            if (enabledSolidColliderCount != 1 || hull == null)
            {
                throw new InvalidOperationException(
                    "Trolley motion requires exactly one enabled non-trigger BoxCollider hull.");
            }

            ValidateVector(hull.center, "trolley hull center");
            ValidateVector(hull.size, "trolley hull size");
            Vector3 hullScale = PositiveScale(hull.transform.lossyScale);
            if (hull.size.x <= 0f || hull.size.y <= 0f || hull.size.z <= 0f ||
                hullScale.x <= 0f || hullScale.y <= 0f || hullScale.z <= 0f)
            {
                throw new InvalidOperationException(
                    "Trolley collision hull must have positive dimensions and scale.");
            }

            return hull;
        }

        private static void ValidateStepOffset(CharacterController sourceController)
        {
            float stepOffset = sourceController.stepOffset;
            float height = sourceController.height;
            if (!IsFinite(stepOffset) || stepOffset <= 0f ||
                !IsFinite(height) || height <= 0f || stepOffset > height)
            {
                throw new InvalidOperationException(
                    "Trolley motion requires a finite positive source step offset " +
                    "not exceeding the controller height.");
            }
        }

        private static void ValidateHullPose(HullPose pose, string owner)
        {
            if (!IsFinite(pose.TransformPosition) || !IsFinite(pose.Center))
                throw new InvalidOperationException($"The {owner} must be finite.");
            ValidateQuaternion(pose.Rotation, owner);
        }

        private static bool ShouldIgnore(Collider candidate,
            Transform trolleyTransform, CharacterController sourceController) =>
            IsInHierarchy(candidate.transform, trolleyTransform) ||
            candidate == sourceController;

        private static bool IsInHierarchy(Transform candidate, Transform root) =>
            candidate == root || candidate.IsChildOf(root);

        private static void EnsureBufferWasNotSaturated(int hitCount,
            int capacity, string operation)
        {
            if (hitCount >= capacity)
            {
                throw new InvalidOperationException(
                    $"Physics {operation} saturated its {capacity}-hit buffer.");
            }
        }

        private static Vector3 PositiveScale(Vector3 scale)
        {
            ValidateVector(scale, "collider scale");
            if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f)
            {
                throw new InvalidOperationException(
                    "Trolley collision queries require a positive collider scale.");
            }

            return scale;
        }

        private static void ValidatePose(Vector3 position, Quaternion rotation,
            string owner)
        {
            if (!IsFinite(position))
                throw new InvalidOperationException($"The {owner} position must be finite.");
            ValidateQuaternion(rotation, owner);
            if (Vector3.Dot(rotation * Vector3.up, Vector3.up) < MinimumUprightDot)
            {
                throw new InvalidOperationException(
                    $"The {owner} rotation must keep the trolley upright.");
            }
        }

        private static void ValidateVector(Vector3 value, string owner)
        {
            if (!IsFinite(value))
                throw new InvalidOperationException($"The {owner} must be finite.");
        }

        private static void ValidateQuaternion(Quaternion value, string owner)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) ||
                !IsFinite(value.z) || !IsFinite(value.w))
            {
                throw new InvalidOperationException($"The {owner} must be finite.");
            }

            float length = Mathf.Sqrt(
                value.x * value.x + value.y * value.y +
                value.z * value.z + value.w * value.w);
            if (length < MinimumQuaternionLength || length > MaximumQuaternionLength)
            {
                throw new InvalidOperationException(
                    $"The {owner} must be a normalized quaternion.");
            }
        }

        private static bool ApproximatelyOne(float value) =>
            Mathf.Abs(value - 1f) <= PoseTolerance;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private readonly struct HullGeometry
        {
            public readonly Vector3 HalfExtents;

            private readonly Vector3 _centerInTrolleySpace;
            private readonly Vector3 _transformPositionInTrolleySpace;
            private readonly Quaternion _rotationInTrolleySpace;

            public HullGeometry(Vector3 centerInTrolleySpace,
                Vector3 transformPositionInTrolleySpace,
                Quaternion rotationInTrolleySpace, Vector3 halfExtents)
            {
                _centerInTrolleySpace = centerInTrolleySpace;
                _transformPositionInTrolleySpace = transformPositionInTrolleySpace;
                _rotationInTrolleySpace = rotationInTrolleySpace;
                HalfExtents = halfExtents;
            }

            public HullPose Resolve(Pose trolleyPose) => new(
                trolleyPose.position +
                trolleyPose.rotation * _transformPositionInTrolleySpace,
                trolleyPose.position +
                trolleyPose.rotation * _centerInTrolleySpace,
                trolleyPose.rotation * _rotationInTrolleySpace);
        }

        private readonly struct HullPose
        {
            public readonly Vector3 TransformPosition;
            public readonly Vector3 Center;
            public readonly Quaternion Rotation;

            public HullPose(Vector3 transformPosition, Vector3 center,
                Quaternion rotation)
            {
                TransformPosition = transformPosition;
                Center = center;
                Rotation = rotation;
            }

            public HullPose Translated(Vector3 displacement) => new(
                TransformPosition + displacement,
                Center + displacement,
                Rotation);

            public static HullPose Lerp(HullPose from, HullPose to,
                float progress) => new(
                Vector3.Lerp(from.TransformPosition, to.TransformPosition, progress),
                Vector3.Lerp(from.Center, to.Center, progress),
                Quaternion.Slerp(from.Rotation, to.Rotation, progress));
        }
    }
}
