using System;
using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public sealed class TrolleyMotionService : ITrolleyMotionService
    {
        private const int InitialQueryCapacity = 64;
        private const int MaxQueryCapacity = 4096;
        private const float PoseTolerance = 0.001f;
        private const float MinimumSweepDistance = 0.0001f;
        private const float PenetrationTolerance = 0.000001f;
        private const float MaximumRotationStep = 5f;
        private const float MinimumQuaternionLength = 0.999f;
        private const float MaximumQuaternionLength = 1.001f;
        private const float MinimumUprightDot = 0.999f;

        private RaycastHit[] _sweepHits =
            new RaycastHit[InitialQueryCapacity];
        private Collider[] _overlapHits =
            new Collider[InitialQueryCapacity];

        public bool TryResolveMove(Rigidbody trolleyBody,
            Collider[] trolleyColliders, CharacterController sourceController,
            Vector3 targetPosition, Quaternion targetRotation,
            out Pose resolvedPose)
        {
            if (sourceController == null)
                throw new ArgumentNullException(nameof(sourceController));
            if (!sourceController.enabled ||
                !sourceController.gameObject.activeInHierarchy)
                throw new InvalidOperationException(
                    "Trolley motion requires an active source CharacterController.");
            ValidateStepOffset(sourceController);
            float stepHeight = sourceController.stepOffset;
            return TryResolveMove(
                trolleyBody, trolleyColliders, sourceController.transform,
                sourceController, stepHeight, targetPosition,
                targetRotation, out resolvedPose, out _);
        }

        public bool TryResolveMove(Rigidbody trolleyBody,
            Collider[] trolleyColliders, Transform sourceTransform,
            float stepHeight, Vector3 targetPosition,
            Quaternion targetRotation, out Pose resolvedPose)
        {
            return TryResolveMove(
                trolleyBody, trolleyColliders, sourceTransform,
                stepHeight, targetPosition, targetRotation,
                out resolvedPose, out _);
        }

        public bool TryResolveMove(Rigidbody trolleyBody,
            Collider[] trolleyColliders, Transform sourceTransform,
            float stepHeight, Vector3 targetPosition,
            Quaternion targetRotation, out Pose resolvedPose,
            out Collider blockingCollider)
        {
            if (sourceTransform == null)
                throw new ArgumentNullException(nameof(sourceTransform));
            if (!sourceTransform.gameObject.activeInHierarchy)
                throw new InvalidOperationException(
                    "Trolley motion requires an active source Transform.");
            if (!IsFinite(stepHeight) || stepHeight < 0f)
                throw new InvalidOperationException(
                    "Trolley motion step height must be finite and non-negative.");
            return TryResolveMove(
                trolleyBody, trolleyColliders, sourceTransform, null,
                stepHeight, targetPosition, targetRotation,
                out resolvedPose, out blockingCollider);
        }

        private bool TryResolveMove(Rigidbody trolleyBody,
            Collider[] trolleyColliders, Transform sourceTransform,
            Collider sourceCollider, float stepHeight,
            Vector3 targetPosition, Quaternion targetRotation,
            out Pose resolvedPose, out Collider blockingCollider)
        {
            BoxCollider hull = ValidateAndGetHull(
                trolleyBody, trolleyColliders, sourceTransform);
            ValidatePose(targetPosition, targetRotation, "target trolley");

            Transform trolleyTransform = trolleyBody.transform;
            ValidatePose(trolleyTransform.position, trolleyTransform.rotation,
                "current trolley");
            if ((trolleyBody.position - trolleyTransform.position).sqrMagnitude >
                PoseTolerance * PoseTolerance ||
                Quaternion.Angle(trolleyBody.rotation, trolleyTransform.rotation) >
                PoseTolerance)
            {
                throw new InvalidOperationException(
                    $"Kinematic Rigidbody '{trolleyBody.name}' and Transform poses " +
                    $"are out of sync: position delta " +
                    $"{Vector3.Distance(trolleyBody.position, trolleyTransform.position):F6}, " +
                    $"rotation delta " +
                    $"{Quaternion.Angle(trolleyBody.rotation, trolleyTransform.rotation):F6} degrees.");
            }

            Pose currentPose = new(trolleyBody.position, trolleyBody.rotation);
            resolvedPose = currentPose;
            blockingCollider = null;
            HullGeometry geometry = CreateHullGeometry(
                trolleyTransform, hull, currentPose);
            Pose targetPose = new(targetPosition, targetRotation);

            if (IsPathClear(
                    hull,
                    geometry,
                    currentPose,
                    targetPose,
                    trolleyTransform,
                    sourceTransform,
                    sourceCollider,
                    out Collider directBlocker))
            {
                resolvedPose = targetPose;
                return true;
            }
            blockingCollider = directBlocker;

            float rise = targetPosition.y - currentPose.position.y;
            if (Mathf.Abs(rise) > stepHeight + PoseTolerance)
                return false;
            if (stepHeight <= PoseTolerance)
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
                    sourceTransform,
                    sourceCollider,
                    out Collider raisedBlocker))
            {
                blockingCollider = raisedBlocker;
                return false;
            }
            if (!IsPathClear(
                    hull,
                    geometry,
                    raisedPose,
                    raisedTargetPose,
                    trolleyTransform,
                    sourceTransform,
                    sourceCollider,
                    out Collider raisedPathBlocker))
            {
                blockingCollider = raisedPathBlocker;
                return false;
            }
            if (!IsPathClear(
                    hull,
                    geometry,
                    raisedTargetPose,
                    targetPose,
                    trolleyTransform,
                    sourceTransform,
                    sourceCollider,
                    out Collider descentBlocker))
            {
                blockingCollider = descentBlocker;
                return false;
            }

            resolvedPose = targetPose;
            blockingCollider = null;
            return true;
        }

        private bool IsPathClear(BoxCollider hull, HullGeometry geometry,
            Pose startPose, Pose targetPose, Transform trolleyTransform,
            Transform sourceTransform, Collider sourceCollider,
            out Collider blockingCollider)
        {
            blockingCollider = null;
            HullPose startHullPose = geometry.Resolve(startPose);
            HullPose targetHullPose = geometry.Resolve(targetPose);
            ValidateHullPose(startHullPose, "start trolley hull");
            ValidateHullPose(targetHullPose, "target trolley hull");
            Vector3 displacement = targetHullPose.Center - startHullPose.Center;
            float distance = displacement.magnitude;

            if (distance > MinimumSweepDistance &&
                TryGetBlockingSweep(
                    hull,
                    startHullPose,
                    geometry.HalfExtents,
                    displacement / distance,
                    distance,
                    trolleyTransform,
                    sourceTransform,
                    sourceCollider,
                    out blockingCollider))
            {
                return false;
            }

            if (TryGetBlockingRotationPath(
                    hull,
                    startHullPose,
                    targetHullPose,
                    geometry.HalfExtents,
                    trolleyTransform,
                    sourceTransform,
                    sourceCollider,
                    out blockingCollider))
            {
                return false;
            }

            return !TryGetBlockingOverlap(
                hull,
                targetHullPose,
                geometry.HalfExtents,
                trolleyTransform,
                sourceTransform,
                sourceCollider,
                "target overlap",
                out blockingCollider);
        }

        private bool TryGetBlockingSweep(BoxCollider hull, HullPose origin,
            Vector3 halfExtents, Vector3 direction, float distance,
            Transform trolleyTransform, Transform sourceTransform,
            Collider sourceCollider, out Collider blockingCollider)
        {
            blockingCollider = null;
            int hitCount = QuerySweep(
                origin, halfExtents, direction, distance);
            float contactProbeDistance = ContactProbeDistance();
            float blockingDistance = float.PositiveInfinity;

            for (int index = 0; index < hitCount; index++)
            {
                Collider hit = _sweepHits[index].collider;
                if (hit == null)
                {
                    throw new InvalidOperationException(
                        "Trolley box cast returned a missing collider.");
                }
                if (ShouldIgnore(hit, trolleyTransform, sourceTransform,
                        sourceCollider))
                    continue;

                float hitDistance = _sweepHits[index].distance;
                if (!IsFinite(hitDistance) || hitDistance < 0f ||
                    hitDistance > distance + PoseTolerance)
                {
                    throw new InvalidOperationException(
                        "Trolley box cast returned an invalid hit distance.");
                }
                bool blocks = hitDistance > PoseTolerance;
                if (!blocks)
                {
                    float probeDistance = Mathf.Min(
                        hitDistance + contactProbeDistance,
                        distance);
                    HullPose probePose = origin.Translated(
                        direction * probeDistance);
                    blocks = PenetrationDepth(hull, probePose, hit) >
                             PenetrationTolerance;
                }
                if (blocks && hitDistance < blockingDistance)
                {
                    blockingCollider = hit;
                    blockingDistance = hitDistance;
                }
            }

            return blockingCollider != null;
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

        private bool TryGetBlockingRotationPath(BoxCollider hull,
            HullPose currentPose, HullPose targetPose, Vector3 halfExtents,
            Transform trolleyTransform, Transform sourceTransform,
            Collider sourceCollider, out Collider blockingCollider)
        {
            blockingCollider = null;
            float rotationAngle = Quaternion.Angle(
                currentPose.Rotation, targetPose.Rotation);
            int stepCount = Mathf.CeilToInt(rotationAngle / MaximumRotationStep);
            for (int step = 1; step < stepCount; step++)
            {
                float progress = (float)step / stepCount;
                if (TryGetBlockingOverlap(
                        hull,
                        HullPose.Lerp(currentPose, targetPose, progress),
                        halfExtents,
                        trolleyTransform,
                        sourceTransform,
                        sourceCollider,
                        "rotation-path overlap",
                        out blockingCollider))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetBlockingOverlap(BoxCollider hull, HullPose pose,
            Vector3 halfExtents, Transform trolleyTransform,
            Transform sourceTransform, Collider sourceCollider,
            string operation, out Collider blockingCollider)
        {
            blockingCollider = null;
            int hitCount = QueryOverlaps(pose, halfExtents, operation);

            for (int index = 0; index < hitCount; index++)
            {
                Collider hit = _overlapHits[index];
                if (hit == null)
                {
                    throw new InvalidOperationException(
                        $"Trolley {operation} returned a missing collider.");
                }
                if (!ShouldIgnore(hit, trolleyTransform, sourceTransform,
                        sourceCollider) &&
                    PenetrationDepth(hull, pose, hit) > PenetrationTolerance)
                {
                    blockingCollider = hit;
                    return true;
                }
            }

            return false;
        }

        private int QuerySweep(HullPose origin, Vector3 halfExtents,
            Vector3 direction, float distance)
        {
            while (true)
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
                if (hitCount < _sweepHits.Length)
                    return hitCount;

                GrowSweepBuffer("trolley box cast");
            }
        }

        private int QueryOverlaps(HullPose pose, Vector3 halfExtents,
            string operation)
        {
            while (true)
            {
                int hitCount = UnityEngine.Physics.OverlapBoxNonAlloc(
                    pose.Center,
                    halfExtents,
                    _overlapHits,
                    pose.Rotation,
                    UnityEngine.Physics.AllLayers,
                    QueryTriggerInteraction.Ignore);
                if (hitCount < _overlapHits.Length)
                    return hitCount;

                GrowOverlapBuffer(operation);
            }
        }

        private void GrowSweepBuffer(string operation)
        {
            if (_sweepHits.Length >= MaxQueryCapacity)
            {
                throw new InvalidOperationException(
                    $"Physics {operation} exceeded {MaxQueryCapacity} nearby colliders.");
            }

            Array.Resize(ref _sweepHits,
                Mathf.Min(_sweepHits.Length * 2, MaxQueryCapacity));
        }

        private void GrowOverlapBuffer(string operation)
        {
            if (_overlapHits.Length >= MaxQueryCapacity)
            {
                throw new InvalidOperationException(
                    $"Physics {operation} exceeded {MaxQueryCapacity} nearby colliders.");
            }

            Array.Resize(ref _overlapHits,
                Mathf.Min(_overlapHits.Length * 2, MaxQueryCapacity));
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
            Collider[] trolleyColliders, Transform sourceTransform)
        {
            if (trolleyBody == null)
                throw new ArgumentNullException(nameof(trolleyBody));
            if (trolleyColliders == null)
                throw new ArgumentNullException(nameof(trolleyColliders));
            if (sourceTransform == null)
                throw new ArgumentNullException(nameof(sourceTransform));
            if (!trolleyBody.gameObject.activeInHierarchy || !trolleyBody.isKinematic ||
                trolleyBody.useGravity || !trolleyBody.detectCollisions)
            {
                throw new InvalidOperationException(
                    "Trolley motion requires an active collision-enabled kinematic Rigidbody.");
            }
            if (!sourceTransform.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException(
                    "Trolley motion requires an active source Transform.");
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
            Transform trolleyTransform, Transform sourceTransform,
            Collider sourceCollider) =>
            IsInHierarchy(candidate.transform, trolleyTransform) ||
            IsInHierarchy(candidate.transform, sourceTransform) ||
            candidate == sourceCollider;

        private static bool IsInHierarchy(Transform candidate, Transform root) =>
            candidate == root || candidate.IsChildOf(root);

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
