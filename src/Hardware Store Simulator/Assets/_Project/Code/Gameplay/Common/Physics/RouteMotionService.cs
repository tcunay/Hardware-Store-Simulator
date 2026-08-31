using System;
using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public sealed class RouteMotionService : IRouteMotionService
    {
        private const int InitialQueryCapacity = 64;
        private const int MaxQueryCapacity = 4096;
        private const float PoseTolerance = 0.001f;
        private const float MinimumSweepDistance = 0.0001f;
        private const float PenetrationTolerance = 0.000001f;
        private const float MaximumRotationStep = 5f;
        private const float MinimumQuaternionLength = 0.999f;
        private const float MaximumQuaternionLength = 1.001f;

        private RaycastHit[] _sweepHits =
            new RaycastHit[InitialQueryCapacity];
        private Collider[] _overlapHits =
            new Collider[InitialQueryCapacity];

        public bool TryResolveMove(Rigidbody body, Collider[] colliders,
            Vector3 targetPosition, Quaternion targetRotation,
            out Pose resolvedPose, out Collider blockingCollider)
        {
            Collider sourceCollider = ValidateAndGetHull(body, colliders);
            ValidatePose(targetPosition, targetRotation, "target route mover");

            Transform root = body.transform;
            ValidatePose(root.position, root.rotation, "current route mover");
            if ((body.position - root.position).sqrMagnitude >
                PoseTolerance * PoseTolerance ||
                Quaternion.Angle(body.rotation, root.rotation) > PoseTolerance)
            {
                throw new InvalidOperationException(
                    $"Kinematic Rigidbody '{body.name}' and Transform poses are out " +
                    $"of sync: position delta " +
                    $"{Vector3.Distance(body.position, root.position):F6}, rotation " +
                    $"delta {Quaternion.Angle(body.rotation, root.rotation):F6} degrees.");
            }

            Pose currentPose = new(body.position, body.rotation);
            Pose targetPose = new(targetPosition, targetRotation);
            resolvedPose = currentPose;
            blockingCollider = null;
            HullGeometry geometry = CreateGeometry(
                sourceCollider, currentPose);

            if (!IsPathClear(
                    geometry, currentPose, targetPose, root,
                    out blockingCollider))
            {
                return false;
            }

            resolvedPose = targetPose;
            blockingCollider = null;
            return true;
        }

        private bool IsPathClear(HullGeometry geometry, Pose startRootPose,
            Pose targetRootPose, Transform root,
            out Collider blockingCollider)
        {
            blockingCollider = null;
            HullPose startHullPose = geometry.Resolve(startRootPose);
            HullPose targetHullPose = geometry.Resolve(targetRootPose);
            ValidateHullPose(startHullPose, "start route-mover hull");
            ValidateHullPose(targetHullPose, "target route-mover hull");

            Vector3 displacement = targetHullPose.Center - startHullPose.Center;
            float distance = displacement.magnitude;
            if (distance > MinimumSweepDistance &&
                TryGetBlockingSweep(
                    geometry,
                    startHullPose,
                    displacement / distance,
                    distance,
                    root,
                    out blockingCollider))
            {
                return false;
            }

            if (TryGetBlockingRotationPath(
                    geometry,
                    startRootPose,
                    targetRootPose,
                    root,
                    out blockingCollider))
            {
                return false;
            }

            return !TryGetBlockingOverlap(
                geometry,
                targetHullPose,
                root,
                "target overlap",
                out blockingCollider);
        }

        private bool TryGetBlockingSweep(HullGeometry geometry,
            HullPose origin, Vector3 direction, float distance,
            Transform root, out Collider blockingCollider)
        {
            blockingCollider = null;
            int hitCount = QuerySweep(
                geometry, origin, direction, distance);

            float contactProbeDistance = ContactProbeDistance();
            float blockingDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                Collider candidate = _sweepHits[index].collider;
                if (candidate == null)
                {
                    throw new InvalidOperationException(
                        "Route-mover sweep returned a missing collider.");
                }
                if (ShouldIgnore(geometry.SourceCollider, candidate, root))
                    continue;

                float hitDistance = _sweepHits[index].distance;
                if (!IsFinite(hitDistance) || hitDistance < 0f ||
                    hitDistance > distance + PoseTolerance)
                {
                    throw new InvalidOperationException(
                        "Route-mover sweep returned an invalid hit distance.");
                }

                bool blocks = hitDistance > PoseTolerance;
                if (!blocks)
                {
                    float probeDistance = Mathf.Min(
                        hitDistance + contactProbeDistance, distance);
                    HullPose probePose = origin.Translated(
                        direction * probeDistance);
                    blocks = PenetrationDepth(
                        geometry.SourceCollider, probePose, candidate) >
                        PenetrationTolerance;
                }
                if (blocks && hitDistance < blockingDistance)
                {
                    blockingCollider = candidate;
                    blockingDistance = hitDistance;
                }
            }

            return blockingCollider != null;
        }

        private bool TryGetBlockingRotationPath(HullGeometry geometry,
            Pose startRootPose, Pose targetRootPose, Transform root,
            out Collider blockingCollider)
        {
            blockingCollider = null;
            float rotationAngle = Quaternion.Angle(
                startRootPose.rotation, targetRootPose.rotation);
            int stepCount = Mathf.CeilToInt(
                rotationAngle / MaximumRotationStep);
            for (int step = 1; step < stepCount; step++)
            {
                float progress = (float)step / stepCount;
                Pose sampleRootPose = new(
                    Vector3.Lerp(
                        startRootPose.position,
                        targetRootPose.position,
                        progress),
                    Quaternion.Slerp(
                        startRootPose.rotation,
                        targetRootPose.rotation,
                        progress));
                if (TryGetBlockingOverlap(
                        geometry,
                        geometry.Resolve(sampleRootPose),
                        root,
                        "rotation-path overlap",
                        out blockingCollider))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetBlockingOverlap(HullGeometry geometry,
            HullPose pose, Transform root, string operation,
            out Collider blockingCollider)
        {
            blockingCollider = null;
            int hitCount = QueryOverlaps(geometry, pose, operation);

            for (int index = 0; index < hitCount; index++)
            {
                Collider candidate = _overlapHits[index];
                if (candidate == null)
                {
                    throw new InvalidOperationException(
                        $"Route-mover {operation} returned a missing collider.");
                }
                if (!ShouldIgnore(
                        geometry.SourceCollider, candidate, root) &&
                    PenetrationDepth(
                        geometry.SourceCollider, pose, candidate) >
                    PenetrationTolerance)
                {
                    blockingCollider = candidate;
                    return true;
                }
            }

            return false;
        }

        private int QuerySweep(HullGeometry geometry, HullPose origin,
            Vector3 direction, float distance)
        {
            while (true)
            {
                int hitCount = geometry.Shape switch
                {
                    HullShape.Box => UnityEngine.Physics.BoxCastNonAlloc(
                        origin.Center,
                        geometry.HalfExtents,
                        direction,
                        _sweepHits,
                        origin.Rotation,
                        distance,
                        UnityEngine.Physics.AllLayers,
                        QueryTriggerInteraction.Ignore),
                    HullShape.Capsule => UnityEngine.Physics.CapsuleCastNonAlloc(
                        origin.CapsulePointA,
                        origin.CapsulePointB,
                        geometry.CapsuleRadius,
                        direction,
                        _sweepHits,
                        distance,
                        UnityEngine.Physics.AllLayers,
                        QueryTriggerInteraction.Ignore),
                    _ => throw new ArgumentOutOfRangeException()
                };
                if (hitCount < _sweepHits.Length)
                    return hitCount;

                GrowSweepBuffer("route-mover sweep");
            }
        }

        private int QueryOverlaps(HullGeometry geometry, HullPose pose,
            string operation)
        {
            while (true)
            {
                int hitCount = geometry.Shape switch
                {
                    HullShape.Box => UnityEngine.Physics.OverlapBoxNonAlloc(
                        pose.Center,
                        geometry.HalfExtents,
                        _overlapHits,
                        pose.Rotation,
                        UnityEngine.Physics.AllLayers,
                        QueryTriggerInteraction.Ignore),
                    HullShape.Capsule => UnityEngine.Physics.OverlapCapsuleNonAlloc(
                        pose.CapsulePointA,
                        pose.CapsulePointB,
                        geometry.CapsuleRadius,
                        _overlapHits,
                        UnityEngine.Physics.AllLayers,
                        QueryTriggerInteraction.Ignore),
                    _ => throw new ArgumentOutOfRangeException()
                };
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

        private static float PenetrationDepth(Collider sourceCollider,
            HullPose sourcePose, Collider candidate)
        {
            if (!UnityEngine.Physics.ComputePenetration(
                    sourceCollider,
                    sourcePose.TransformPosition,
                    sourcePose.Rotation,
                    candidate,
                    candidate.transform.position,
                    candidate.transform.rotation,
                    out Vector3 separationDirection,
                    out float separationDistance))
            {
                return 0f;
            }

            ValidateVector(
                separationDirection, "route-mover separation direction");
            if (!IsFinite(separationDistance) || separationDistance < 0f)
            {
                throw new InvalidOperationException(
                    "Route-mover penetration distance must be finite and " +
                    "non-negative.");
            }

            return separationDistance;
        }

        private static HullGeometry CreateGeometry(Collider sourceCollider,
            Pose currentRootPose)
        {
            Transform colliderTransform = sourceCollider.transform;
            Quaternion rotationInRoot =
                Quaternion.Inverse(currentRootPose.rotation) *
                colliderTransform.rotation;
            Vector3 transformPositionInRoot =
                Quaternion.Inverse(currentRootPose.rotation) *
                (colliderTransform.position - currentRootPose.position);

            if (sourceCollider is BoxCollider box)
            {
                Vector3 centerInRoot =
                    Quaternion.Inverse(currentRootPose.rotation) *
                    (colliderTransform.TransformPoint(box.center) -
                     currentRootPose.position);
                Vector3 halfExtents = Vector3.Scale(
                    box.size * 0.5f,
                    PositiveScale(colliderTransform.lossyScale));
                ValidateVector(halfExtents, "route-mover box half extents");
                if (halfExtents.x <= 0f || halfExtents.y <= 0f ||
                    halfExtents.z <= 0f)
                {
                    throw new InvalidOperationException(
                        "Route-mover box hull must have positive dimensions.");
                }

                return HullGeometry.Box(
                    sourceCollider,
                    transformPositionInRoot,
                    centerInRoot,
                    rotationInRoot,
                    halfExtents);
            }

            if (sourceCollider is CapsuleCollider capsule)
            {
                Vector3 centerInRoot =
                    Quaternion.Inverse(currentRootPose.rotation) *
                    (colliderTransform.TransformPoint(capsule.center) -
                     currentRootPose.position);
                Vector3 scale = PositiveScale(colliderTransform.lossyScale);
                ResolveCapsuleScale(
                    capsule.direction,
                    scale,
                    out float axisScale,
                    out float radiusScale);
                float radius = capsule.radius * radiusScale;
                float scaledHeight = capsule.height * axisScale;
                if (scaledHeight + PoseTolerance < radius * 2f)
                {
                    throw new InvalidOperationException(
                        "Route-mover capsule height cannot be smaller than its " +
                        "scaled diameter.");
                }
                float segmentHalfLength = Mathf.Max(
                    0f, scaledHeight * 0.5f - radius);
                if (!IsFinite(radius) || radius <= 0f ||
                    !IsFinite(segmentHalfLength))
                {
                    throw new InvalidOperationException(
                        "Route-mover capsule hull must have positive finite dimensions.");
                }

                return HullGeometry.Capsule(
                    sourceCollider,
                    transformPositionInRoot,
                    centerInRoot,
                    rotationInRoot,
                    CapsuleAxis(capsule.direction),
                    radius,
                    segmentHalfLength);
            }

            throw new InvalidOperationException(
                $"Route mover collider '{sourceCollider.name}' must be a " +
                "BoxCollider or CapsuleCollider.");
        }

        private static Collider ValidateAndGetHull(Rigidbody body,
            Collider[] colliders)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));
            if (colliders == null)
                throw new ArgumentNullException(nameof(colliders));
            if (!body.gameObject.activeInHierarchy || !body.isKinematic ||
                body.useGravity || !body.detectCollisions)
            {
                throw new InvalidOperationException(
                    "Route motion requires an active collision-enabled kinematic " +
                    "Rigidbody without gravity.");
            }

            Vector3 rootScale = body.transform.lossyScale;
            ValidateVector(rootScale, "route-mover root scale");
            if (!ApproximatelyOne(rootScale.x) ||
                !ApproximatelyOne(rootScale.y) ||
                !ApproximatelyOne(rootScale.z))
            {
                throw new InvalidOperationException(
                    "Route motion requires a unit world scale on the Rigidbody root.");
            }

            Collider hull = null;
            int enabledSolidColliderCount = 0;
            foreach (Collider collider in colliders)
            {
                if (collider == null)
                {
                    throw new InvalidOperationException(
                        "Route mover Colliders contains a missing reference.");
                }
                if (collider.attachedRigidbody != body ||
                    !IsInHierarchy(collider.transform, body.transform))
                {
                    throw new InvalidOperationException(
                        $"Route mover collider '{collider.name}' is not attached " +
                        "to its Rigidbody root.");
                }
                if (!collider.enabled || !collider.gameObject.activeInHierarchy ||
                    collider.isTrigger)
                {
                    continue;
                }

                enabledSolidColliderCount++;
                hull = collider;
            }

            if (enabledSolidColliderCount != 1 || hull == null)
            {
                throw new InvalidOperationException(
                    "Route motion requires exactly one active enabled solid collider.");
            }
            if (hull is not BoxCollider && hull is not CapsuleCollider)
            {
                throw new InvalidOperationException(
                    $"Route mover collider '{hull.name}' must be a BoxCollider " +
                    "or CapsuleCollider.");
            }

            return hull;
        }

        private static bool ShouldIgnore(Collider source,
            Collider candidate, Transform root) =>
            !candidate.enabled || candidate.isTrigger ||
            !candidate.gameObject.activeInHierarchy ||
            candidate.attachedRigidbody == source.attachedRigidbody ||
            IsInHierarchy(candidate.transform, root) ||
            UnityEngine.Physics.GetIgnoreLayerCollision(
                source.gameObject.layer, candidate.gameObject.layer) ||
            UnityEngine.Physics.GetIgnoreCollision(source, candidate);

        private static bool IsInHierarchy(Transform candidate,
            Transform root) =>
            candidate == root || candidate.IsChildOf(root);

        private static void ResolveCapsuleScale(int direction,
            Vector3 scale, out float axisScale, out float radiusScale)
        {
            switch (direction)
            {
                case 0:
                    axisScale = scale.x;
                    radiusScale = Mathf.Max(scale.y, scale.z);
                    return;
                case 1:
                    axisScale = scale.y;
                    radiusScale = Mathf.Max(scale.x, scale.z);
                    return;
                case 2:
                    axisScale = scale.z;
                    radiusScale = Mathf.Max(scale.x, scale.y);
                    return;
                default:
                    throw new InvalidOperationException(
                        $"Capsule collider direction {direction} is invalid.");
            }
        }

        private static Vector3 CapsuleAxis(int direction) => direction switch
        {
            0 => Vector3.right,
            1 => Vector3.up,
            2 => Vector3.forward,
            _ => throw new InvalidOperationException(
                $"Capsule collider direction {direction} is invalid.")
        };

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

        private static Vector3 PositiveScale(Vector3 scale)
        {
            ValidateVector(scale, "route-mover collider scale");
            if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f)
            {
                throw new InvalidOperationException(
                    "Route motion requires a positive collider scale.");
            }

            return scale;
        }

        private static void ValidateHullPose(HullPose pose, string owner)
        {
            ValidateVector(pose.TransformPosition, owner);
            ValidateVector(pose.Center, owner);
            ValidateVector(pose.CapsulePointA, owner);
            ValidateVector(pose.CapsulePointB, owner);
            ValidateQuaternion(pose.Rotation, owner);
        }

        private static void ValidatePose(Vector3 position,
            Quaternion rotation, string owner)
        {
            ValidateVector(position, $"{owner} position");
            ValidateQuaternion(rotation, $"{owner} rotation");
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
            if (length < MinimumQuaternionLength ||
                length > MaximumQuaternionLength)
            {
                throw new InvalidOperationException(
                    $"The {owner} must be a normalized quaternion.");
            }
        }

        private static bool ApproximatelyOne(float value) =>
            Mathf.Abs(value - 1f) <= PoseTolerance;

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private enum HullShape
        {
            Box = 0,
            Capsule = 1
        }

        private readonly struct HullGeometry
        {
            public readonly Collider SourceCollider;
            public readonly HullShape Shape;
            public readonly Vector3 HalfExtents;
            public readonly float CapsuleRadius;

            private readonly Vector3 _transformPositionInRoot;
            private readonly Vector3 _centerInRoot;
            private readonly Quaternion _rotationInRoot;
            private readonly Vector3 _capsuleAxis;
            private readonly float _capsuleSegmentHalfLength;

            private HullGeometry(Collider sourceCollider, HullShape shape,
                Vector3 transformPositionInRoot, Vector3 centerInRoot,
                Quaternion rotationInRoot, Vector3 halfExtents,
                Vector3 capsuleAxis, float capsuleRadius,
                float capsuleSegmentHalfLength)
            {
                SourceCollider = sourceCollider;
                Shape = shape;
                _transformPositionInRoot = transformPositionInRoot;
                _centerInRoot = centerInRoot;
                _rotationInRoot = rotationInRoot;
                HalfExtents = halfExtents;
                _capsuleAxis = capsuleAxis;
                CapsuleRadius = capsuleRadius;
                _capsuleSegmentHalfLength = capsuleSegmentHalfLength;
            }

            public static HullGeometry Box(Collider sourceCollider,
                Vector3 transformPositionInRoot, Vector3 centerInRoot,
                Quaternion rotationInRoot, Vector3 halfExtents) => new(
                sourceCollider,
                HullShape.Box,
                transformPositionInRoot,
                centerInRoot,
                rotationInRoot,
                halfExtents,
                Vector3.up,
                0f,
                0f);

            public static HullGeometry Capsule(Collider sourceCollider,
                Vector3 transformPositionInRoot, Vector3 centerInRoot,
                Quaternion rotationInRoot, Vector3 capsuleAxis,
                float capsuleRadius, float capsuleSegmentHalfLength) => new(
                sourceCollider,
                HullShape.Capsule,
                transformPositionInRoot,
                centerInRoot,
                rotationInRoot,
                Vector3.zero,
                capsuleAxis,
                capsuleRadius,
                capsuleSegmentHalfLength);

            public HullPose Resolve(Pose rootPose)
            {
                Quaternion rotation = rootPose.rotation * _rotationInRoot;
                Vector3 center = rootPose.position +
                    rootPose.rotation * _centerInRoot;
                Vector3 capsuleOffset =
                    rotation * _capsuleAxis * _capsuleSegmentHalfLength;
                return new HullPose(
                    rootPose.position +
                    rootPose.rotation * _transformPositionInRoot,
                    center,
                    rotation,
                    center - capsuleOffset,
                    center + capsuleOffset);
            }
        }

        private readonly struct HullPose
        {
            public readonly Vector3 TransformPosition;
            public readonly Vector3 Center;
            public readonly Quaternion Rotation;
            public readonly Vector3 CapsulePointA;
            public readonly Vector3 CapsulePointB;

            public HullPose(Vector3 transformPosition, Vector3 center,
                Quaternion rotation, Vector3 capsulePointA,
                Vector3 capsulePointB)
            {
                TransformPosition = transformPosition;
                Center = center;
                Rotation = rotation;
                CapsulePointA = capsulePointA;
                CapsulePointB = capsulePointB;
            }

            public HullPose Translated(Vector3 displacement) => new(
                TransformPosition + displacement,
                Center + displacement,
                Rotation,
                CapsulePointA + displacement,
                CapsulePointB + displacement);
        }
    }
}
