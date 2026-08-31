using System;
using System.Collections.Generic;
using HardwareStore.Gameplay.Common.Collisions;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;
using UnityEngine.AI;

namespace HardwareStore.Gameplay.Common.Traffic
{
    public sealed class LocalTrafficPredictionService :
        ILocalTrafficPredictionService
    {
        private const int InitialWorldQueryCapacity = 128;
        private const int MaxWorldQueryCapacity = 4096;
        private const float GeometryTolerance = 0.0001f;
        private const float SupportSurfaceTolerance = 0.25f;

        private readonly LocalTrafficConfig _config;
        private readonly ICollisionRegistry _collisions;
        private readonly List<PlanarFootprint> _moverFootprints = new(4);
        private readonly List<PlanarFootprint> _obstacleFootprints = new(4);
        private Collider[] _worldHits =
            new Collider[InitialWorldQueryCapacity];

        public LocalTrafficPredictionService(IStaticDataService staticData,
            ICollisionRegistry collisions)
        {
            _config = staticData.LocalTraffic;
            _collisions = collisions;
        }

        public bool TryPredictConflict(GameEntity mover,
            GameEntity moverMotionOwner, Vector3 moverVelocity,
            float moverIntentDistance, GameEntity obstacle,
            GameEntity obstacleMotionOwner, Vector3 obstacleVelocity,
            float obstacleIntentDistance, bool useReleaseClearance,
            out LocalTrafficPrediction prediction)
        {
            ValidateMotion(mover, moverVelocity, moverIntentDistance);
            ValidateMotion(obstacle, obstacleVelocity, obstacleIntentDistance);
            ValidateMotionOwner(mover, moverMotionOwner);
            ValidateMotionOwner(obstacle, obstacleMotionOwner);
            if (ReferenceEquals(mover, obstacle))
                throw new InvalidOperationException(
                    "Local traffic prediction requires two different entities.");

            CollectFootprints(mover, _moverFootprints);
            CollectFootprints(obstacle, _obstacleFootprints);
            float clearance = useReleaseClearance
                ? _config.ReleaseClearance
                : _config.SafetyClearance;
            float horizon = _config.PredictionHorizon;
            int stepCount = _config.PredictionStepCount;
            float moverDuration = TravelDuration(
                moverVelocity, moverIntentDistance, horizon);
            float obstacleDuration = TravelDuration(
                obstacleVelocity, obstacleIntentDistance, horizon);

            for (int step = 0; step <= stepCount; step++)
            {
                float time = horizon * step / stepCount;
                Vector3 moverOffset = moverVelocity * Mathf.Min(time, moverDuration);
                Vector3 obstacleOffset = obstacleVelocity *
                    Mathf.Min(time, obstacleDuration);
                Quaternion moverRotation = PredictRotationDelta(
                    moverMotionOwner, time, respectYieldState: false);
                Quaternion obstacleRotation = PredictRotationDelta(
                    obstacleMotionOwner, time, respectYieldState: true);
                if (!TryFindOverlapAt(
                        _moverFootprints,
                        moverMotionOwner.Transform.position,
                        moverOffset,
                        moverRotation,
                        _obstacleFootprints,
                        obstacleMotionOwner.Transform.position,
                        obstacleOffset,
                        obstacleRotation,
                        clearance,
                        out PlanarFootprint moverFootprint,
                        out PlanarFootprint obstacleFootprint))
                {
                    continue;
                }

                CalculateArrivalTimes(
                    moverFootprint,
                    moverVelocity,
                    moverDuration,
                    obstacleFootprint,
                    obstacleVelocity,
                    obstacleDuration,
                    clearance,
                    horizon,
                    out float moverArrival,
                    out float obstacleArrival);
                prediction = new LocalTrafficPrediction(
                    time, moverArrival, obstacleArrival);
                return true;
            }

            prediction = default;
            return false;
        }

        public bool TryPredictWorldConflict(GameEntity mover,
            GameEntity moverMotionOwner, Vector3 moverVelocity,
            float moverIntentDistance,
            Transform additionalIgnoredRoot,
            bool useReleaseClearance,
            out LocalTrafficWorldPrediction prediction)
        {
            ValidateMotion(mover, moverVelocity, moverIntentDistance);
            ValidateMotionOwner(mover, moverMotionOwner);
            CollectFootprints(mover, _moverFootprints);
            float clearance = useReleaseClearance
                ? _config.ReleaseClearance
                : _config.SafetyClearance;
            float horizon = _config.PredictionHorizon;
            int stepCount = _config.PredictionStepCount;
            float moverDuration = TravelDuration(
                moverVelocity, moverIntentDistance, horizon);

            for (int step = 0; step <= stepCount; step++)
            {
                float time = horizon * step / stepCount;
                Vector3 offset = moverVelocity * Mathf.Min(time, moverDuration);
                Quaternion rotation = PredictRotationDelta(
                    moverMotionOwner, time, respectYieldState: false);
                foreach (PlanarFootprint footprint in _moverFootprints)
                {
                    PlanarFootprint moved = footprint.Transformed(
                        moverMotionOwner.Transform.position,
                        offset,
                        rotation);
                    if (!TryFindWorldObstacle(mover, additionalIgnoredRoot,
                            moved, clearance,
                            out Collider collider))
                    {
                        continue;
                    }

                    prediction = new LocalTrafficWorldPrediction(
                        time, collider);
                    return true;
                }
            }

            prediction = default;
            return false;
        }

        private bool TryFindWorldObstacle(GameEntity mover,
            Transform additionalIgnoredRoot, PlanarFootprint footprint,
            float clearance,
            out Collider obstacle)
        {
            int hitCount = QueryWorldColliders(
                footprint.Center,
                footprint.ResolveWorldQueryHalfExtents(clearance));

            for (int index = 0; index < hitCount; index++)
            {
                Collider candidate = _worldHits[index];
                if (candidate == null)
                    throw new InvalidOperationException(
                        "Local traffic world query returned a missing collider.");
                if (ShouldIgnoreWorldCollider(
                        mover,
                        additionalIgnoredRoot,
                        footprint,
                        candidate))
                    continue;

                PlanarFootprint candidateFootprint = CreateFootprint(candidate);
                if (!footprint.Overlaps(candidateFootprint, clearance))
                    continue;

                obstacle = candidate;
                return true;
            }

            obstacle = null;
            return false;
        }

        private int QueryWorldColliders(Vector3 center, Vector3 halfExtents)
        {
            while (true)
            {
                int hitCount = UnityEngine.Physics.OverlapBoxNonAlloc(
                    center,
                    halfExtents,
                    _worldHits,
                    Quaternion.identity,
                    UnityEngine.Physics.AllLayers,
                    QueryTriggerInteraction.Ignore);
                if (hitCount < _worldHits.Length)
                    return hitCount;
                if (_worldHits.Length >= MaxWorldQueryCapacity)
                {
                    throw new InvalidOperationException(
                        $"Local traffic world query exceeded " +
                        $"{MaxWorldQueryCapacity} nearby colliders.");
                }

                Array.Resize(
                    ref _worldHits,
                    Mathf.Min(_worldHits.Length * 2,
                        MaxWorldQueryCapacity));
            }
        }

        private bool ShouldIgnoreWorldCollider(GameEntity mover,
            Transform additionalIgnoredRoot,
            PlanarFootprint footprint, Collider candidate)
        {
            if (!candidate.enabled || candidate.isTrigger ||
                !candidate.gameObject.activeInHierarchy)
            {
                return true;
            }
            if (candidate.transform.IsChildOf(mover.Transform) ||
                additionalIgnoredRoot != null &&
                candidate.transform.IsChildOf(additionalIgnoredRoot))
                return true;
            if (candidate.bounds.max.y <=
                footprint.Bottom + SupportSurfaceTolerance)
            {
                return true;
            }
            if (!_collisions.TryGet(
                    candidate.GetEntityId(), out GameEntity registered))
            {
                return false;
            }

            return ReferenceEquals(registered, mover) ||
                   registered.isTrafficParticipant && !registered.isDestructed;
        }

        private static void ValidateMotion(GameEntity entity,
            Vector3 velocity, float intentDistance)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            if (!entity.isTrafficParticipant || !entity.hasEntityId ||
                !entity.hasTransform || entity.isDestructed)
            {
                throw new InvalidOperationException(
                    "Traffic prediction requires a live bound traffic participant.");
            }
            if (!IsFinite(velocity) || !IsFinite(intentDistance) ||
                intentDistance < 0f)
            {
                throw new InvalidOperationException(
                    $"Traffic participant {entity.EntityId} has invalid motion intent.");
            }
        }

        private static void ValidateMotionOwner(GameEntity footprint,
            GameEntity motionOwner)
        {
            if (motionOwner == null || motionOwner.isDestructed ||
                !motionOwner.isTrafficParticipant || !motionOwner.hasEntityId ||
                !motionOwner.hasTransform || !motionOwner.hasTrafficAngularIntent)
            {
                throw new InvalidOperationException(
                    $"Traffic footprint {footprint.EntityId} has an invalid motion owner.");
            }
            if (!ReferenceEquals(footprint, motionOwner) &&
                (!footprint.hasTrolleyPusherEntityId ||
                 footprint.TrolleyPusherEntityId != motionOwner.EntityId))
            {
                throw new InvalidOperationException(
                    $"Traffic footprint {footprint.EntityId} is not coupled to motion owner " +
                    $"{motionOwner.EntityId}.");
            }
        }

        private static float TravelDuration(Vector3 velocity,
            float intentDistance, float horizon)
        {
            float speed = Horizontal(velocity).magnitude;
            if (speed <= GeometryTolerance || intentDistance <= GeometryTolerance)
                return 0f;

            return Mathf.Min(horizon, intentDistance / speed);
        }

        private Quaternion PredictRotationDelta(GameEntity entity,
            float time, bool respectYieldState)
        {
            if (Mathf.Abs(entity.TrafficAngularIntent) <= GeometryTolerance)
            {
                return Quaternion.identity;
            }
            if (entity.isPushingTrolley || entity.isPushingWorkerTrolley)
            {
                if (respectYieldState && entity.isTrafficYielding)
                    return Quaternion.identity;

                return Quaternion.AngleAxis(
                    entity.TrafficAngularIntent *
                    Mathf.Clamp01(time / _config.PredictionHorizon),
                    Vector3.up);
            }
            if (entity.isRouteMover && entity.hasRoute &&
                entity.hasRouteWaypointIndex && entity.hasRotationSpeed &&
                !entity.isRouteCompleted)
            {
                Pose[] route = entity.Route;
                int waypointIndex = entity.RouteWaypointIndex;
                if (route == null || waypointIndex <= 0 ||
                    waypointIndex >= route.Length)
                {
                    throw new InvalidOperationException(
                        $"Traffic route mover {entity.EntityId} has an invalid rotation target.");
                }

                float angularBudget = ResolveRouteAngularBudget(
                    entity, time, respectYieldState);
                return RotationDeltaTowards(
                    entity,
                    route[waypointIndex].rotation,
                    angularBudget);
            }
            if (entity.hasNavigationAgent &&
                entity.TrafficDesiredVelocity.sqrMagnitude >
                GeometryTolerance * GeometryTolerance)
            {
                if (respectYieldState && entity.isTrafficYielding)
                    return Quaternion.identity;

                Vector3 direction = new(
                    entity.TrafficDesiredVelocity.x,
                    0f,
                    entity.TrafficDesiredVelocity.z);
                Quaternion target = Quaternion.LookRotation(
                    direction.normalized, Vector3.up);
                return RotationDeltaTowards(
                    entity,
                    target,
                    entity.NavigationAgent.angularSpeed * time);
            }
            throw new InvalidOperationException(
                $"Traffic participant {entity.EntityId} has unsupported angular intent.");
        }

        private float ResolveRouteAngularBudget(GameEntity entity,
            float time, bool respectYieldState)
        {
            if (!entity.hasTrafficCurrentSpeed || !entity.hasMovementSpeed ||
                entity.MovementSpeed <= GeometryTolerance)
            {
                throw new InvalidOperationException(
                    $"Route traffic participant {entity.EntityId} has invalid speed state.");
            }
            if (!IsFinite(time) || time < 0f ||
                !IsFinite(entity.TrafficCurrentSpeed) ||
                entity.TrafficCurrentSpeed < 0f)
            {
                throw new InvalidOperationException(
                    $"Traffic participant {entity.EntityId} has invalid rotation intent.");
            }

            float targetSpeed = respectYieldState && entity.isTrafficYielding
                ? 0f
                : entity.MovementSpeed;
            float rate = targetSpeed < entity.TrafficCurrentSpeed
                ? _config.VehicleBraking
                : _config.VehicleAcceleration;
            float speedDelta = Mathf.Abs(targetSpeed - entity.TrafficCurrentSpeed);
            float transitionTime = rate <= GeometryTolerance
                ? 0f
                : speedDelta / rate;
            float acceleratedTime = Mathf.Min(time, transitionTime);
            float acceleratedEndSpeed = Mathf.MoveTowards(
                entity.TrafficCurrentSpeed,
                targetSpeed,
                rate * acceleratedTime);
            float travelledDistance =
                (entity.TrafficCurrentSpeed + acceleratedEndSpeed) * 0.5f *
                acceleratedTime +
                targetSpeed * Mathf.Max(0f, time - transitionTime);
            return entity.RotationSpeed / entity.MovementSpeed *
                   travelledDistance;
        }

        private static Quaternion RotationDeltaTowards(GameEntity entity,
            Quaternion target, float angularBudget)
        {
            if (!IsFinite(angularBudget) || angularBudget < 0f)
            {
                throw new InvalidOperationException(
                    $"Traffic participant {entity.EntityId} has invalid rotation intent.");
            }
            Quaternion current = entity.Transform.rotation;
            Quaternion predicted = Quaternion.RotateTowards(
                current,
                target,
                angularBudget);
            return predicted * Quaternion.Inverse(current);
        }

        private static void CalculateArrivalTimes(
            PlanarFootprint moverFootprint,
            Vector3 moverVelocity, float moverDuration,
            PlanarFootprint obstacleFootprint,
            Vector3 obstacleVelocity,
            float obstacleDuration, float clearance,
            float horizon,
            out float moverArrival, out float obstacleArrival)
        {
            Vector2 moverStart = Horizontal(moverFootprint.Center);
            Vector2 obstacleStart = Horizontal(obstacleFootprint.Center);
            Vector2 moverEnd = moverStart +
                Horizontal(moverVelocity) * moverDuration;
            Vector2 obstacleEnd = obstacleStart +
                Horizontal(obstacleVelocity) * obstacleDuration;
            ClosestSegmentParameters(
                moverStart,
                moverEnd,
                obstacleStart,
                obstacleEnd,
                out float moverProgress,
                out float obstacleProgress);
            moverArrival = LeadingEdgeArrival(
                moverFootprint,
                moverVelocity,
                moverDuration,
                moverProgress,
                clearance);
            obstacleArrival = LeadingEdgeArrival(
                obstacleFootprint,
                obstacleVelocity,
                obstacleDuration,
                obstacleProgress,
                clearance);
            moverArrival = Mathf.Min(moverArrival, horizon);
            obstacleArrival = Mathf.Min(obstacleArrival, horizon);
        }

        private static float LeadingEdgeArrival(PlanarFootprint footprint,
            Vector3 velocity, float duration, float progress,
            float clearance)
        {
            Vector2 horizontalVelocity = Horizontal(velocity);
            float speed = horizontalVelocity.magnitude;
            if (duration <= GeometryTolerance || speed <= GeometryTolerance)
                return 0f;

            float leadingDistance = footprint.ProjectedRadius(
                horizontalVelocity / speed) + clearance * 0.5f;
            return Mathf.Max(0f,
                duration * progress - leadingDistance / speed);
        }

        private static void ClosestSegmentParameters(Vector2 firstStart,
            Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd,
            out float firstProgress, out float secondProgress)
        {
            Vector2 firstDirection = firstEnd - firstStart;
            Vector2 secondDirection = secondEnd - secondStart;
            Vector2 offset = firstStart - secondStart;
            float firstLength = Vector2.Dot(firstDirection, firstDirection);
            float secondLength = Vector2.Dot(secondDirection, secondDirection);
            float directionsDot = Vector2.Dot(firstDirection, secondDirection);
            float firstOffsetDot = Vector2.Dot(firstDirection, offset);
            float secondOffsetDot = Vector2.Dot(secondDirection, offset);

            if (firstLength <= GeometryTolerance &&
                secondLength <= GeometryTolerance)
            {
                firstProgress = 0f;
                secondProgress = 0f;
                return;
            }
            if (firstLength <= GeometryTolerance)
            {
                firstProgress = 0f;
                secondProgress = Mathf.Clamp01(secondOffsetDot / secondLength);
                return;
            }
            if (secondLength <= GeometryTolerance)
            {
                secondProgress = 0f;
                firstProgress = Mathf.Clamp01(-firstOffsetDot / firstLength);
                return;
            }

            float denominator = firstLength * secondLength -
                                directionsDot * directionsDot;
            if (denominator <=
                GeometryTolerance * firstLength * secondLength)
            {
                ResolveParallelSegmentParameters(
                    firstStart,
                    firstDirection,
                    secondStart,
                    secondDirection,
                    out firstProgress,
                    out secondProgress);
                return;
            }

            firstProgress = Mathf.Clamp01(
                (directionsDot * secondOffsetDot -
                 firstOffsetDot * secondLength) / denominator);
            secondProgress = (directionsDot * firstProgress +
                              secondOffsetDot) / secondLength;
            if (secondProgress < 0f)
            {
                secondProgress = 0f;
                firstProgress = Mathf.Clamp01(-firstOffsetDot / firstLength);
            }
            else if (secondProgress > 1f)
            {
                secondProgress = 1f;
                firstProgress = Mathf.Clamp01(
                    (directionsDot - firstOffsetDot) / firstLength);
            }
        }

        private static void ResolveParallelSegmentParameters(
            Vector2 firstStart, Vector2 firstDirection,
            Vector2 secondStart, Vector2 secondDirection,
            out float firstProgress, out float secondProgress)
        {
            Vector2 firstAxis = CanonicalAxis(firstDirection.normalized);
            Vector2 secondAxis = CanonicalAxis(secondDirection.normalized);
            Vector2 axis = firstAxis + secondAxis;
            if (axis.sqrMagnitude <= GeometryTolerance * GeometryTolerance)
                axis = firstAxis;
            else
                axis.Normalize();
            axis = CanonicalAxis(axis);

            float firstStartProjection = Vector2.Dot(firstStart, axis);
            float firstEndProjection = Vector2.Dot(
                firstStart + firstDirection, axis);
            float secondStartProjection = Vector2.Dot(secondStart, axis);
            float secondEndProjection = Vector2.Dot(
                secondStart + secondDirection, axis);
            float sharedStart = Mathf.Max(
                Mathf.Min(firstStartProjection, firstEndProjection),
                Mathf.Min(secondStartProjection, secondEndProjection));
            float sharedEnd = Mathf.Min(
                Mathf.Max(firstStartProjection, firstEndProjection),
                Mathf.Max(secondStartProjection, secondEndProjection));
            float conflictProjection = (sharedStart + sharedEnd) * 0.5f;

            firstProgress = ProjectedProgress(
                firstStartProjection,
                Vector2.Dot(firstDirection, axis),
                conflictProjection);
            secondProgress = ProjectedProgress(
                secondStartProjection,
                Vector2.Dot(secondDirection, axis),
                conflictProjection);
        }

        private static Vector2 CanonicalAxis(Vector2 axis)
        {
            if (axis.x < -GeometryTolerance ||
                Mathf.Abs(axis.x) <= GeometryTolerance && axis.y < 0f)
            {
                return -axis;
            }

            return axis;
        }

        private static float ProjectedProgress(float start,
            float projectedDirection, float conflict)
        {
            if (Mathf.Abs(projectedDirection) <= GeometryTolerance)
            {
                throw new InvalidOperationException(
                    "Parallel traffic segment has a degenerate canonical projection.");
            }

            return Mathf.Clamp01((conflict - start) / projectedDirection);
        }

        private static bool TryFindOverlapAt(
            IReadOnlyList<PlanarFootprint> first,
            Vector3 firstPivot,
            Vector3 firstOffset,
            Quaternion firstRotation,
            IReadOnlyList<PlanarFootprint> second,
            Vector3 secondPivot,
            Vector3 secondOffset,
            Quaternion secondRotation,
            float clearance,
            out PlanarFootprint firstOverlap,
            out PlanarFootprint secondOverlap)
        {
            for (int firstIndex = 0; firstIndex < first.Count; firstIndex++)
            for (int secondIndex = 0; secondIndex < second.Count; secondIndex++)
            {
                PlanarFootprint a = first[firstIndex].Transformed(
                    firstPivot, firstOffset, firstRotation);
                PlanarFootprint b = second[secondIndex].Transformed(
                    secondPivot, secondOffset, secondRotation);
                if (a.Overlaps(b, clearance))
                {
                    firstOverlap = first[firstIndex];
                    secondOverlap = second[secondIndex];
                    return true;
                }
            }

            firstOverlap = default;
            secondOverlap = default;
            return false;
        }

        private static void CollectFootprints(GameEntity entity,
            List<PlanarFootprint> footprints)
        {
            footprints.Clear();
            if (entity.hasColliders)
            {
                foreach (Collider collider in entity.Colliders)
                {
                    if (collider == null)
                    {
                        throw new InvalidOperationException(
                            $"Traffic participant {entity.EntityId} has a missing collider.");
                    }
                    if (!collider.enabled || collider.isTrigger ||
                        !collider.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    footprints.Add(CreateFootprint(collider));
                }
            }
            if (footprints.Count != 0)
                return;
            if (entity.hasRigidbody)
            {
                Collider[] rigidbodyColliders =
                    entity.Rigidbody.GetComponentsInChildren<Collider>(
                        includeInactive: true);
                foreach (Collider collider in rigidbodyColliders)
                {
                    if (!collider.enabled || collider.isTrigger ||
                        !collider.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    footprints.Add(CreateFootprint(collider));
                }
                if (footprints.Count != 0)
                    return;
            }
            if (entity.hasCharacterController)
            {
                CharacterController controller = entity.CharacterController;
                if (!controller.enabled ||
                    !controller.gameObject.activeInHierarchy)
                {
                    throw new InvalidOperationException(
                        $"Traffic participant {entity.EntityId} has an inactive controller.");
                }

                footprints.Add(CreateFootprint(controller));
                return;
            }
            if (entity.hasNavigationAgent)
            {
                NavMeshAgent agent = entity.NavigationAgent;
                Vector3 scale = agent.transform.lossyScale;
                float radius = agent.radius * Mathf.Max(
                    Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                float halfHeight = agent.height * Mathf.Abs(scale.y) * 0.5f;
                footprints.Add(PlanarFootprint.Circle(
                    agent.transform.TransformPoint(new Vector3(
                        0f,
                        agent.baseOffset + agent.height * 0.5f,
                        0f)),
                    radius,
                    halfHeight));
                return;
            }

            throw new InvalidOperationException(
                $"Traffic participant {entity.EntityId} has no enabled solid footprint.");
        }

        private static PlanarFootprint CreateFootprint(Collider collider)
        {
            if (collider is BoxCollider box)
            {
                Transform transform = box.transform;
                Vector3 xExtent = transform.TransformVector(
                    Vector3.right * box.size.x * 0.5f);
                Vector3 zExtent = transform.TransformVector(
                    Vector3.forward * box.size.z * 0.5f);
                Vector2 axisX = Horizontal(xExtent);
                Vector2 axisZ = Horizontal(zExtent);
                float halfX = axisX.magnitude;
                float halfZ = axisZ.magnitude;
                if (halfX <= GeometryTolerance || halfZ <= GeometryTolerance)
                {
                    throw new InvalidOperationException(
                        $"Traffic box collider '{box.name}' has a degenerate footprint.");
                }
                axisX /= halfX;
                axisZ /= halfZ;
                float halfHeight = Mathf.Abs(
                    transform.TransformVector(Vector3.up * box.size.y * 0.5f).y);
                return PlanarFootprint.Box(
                    transform.TransformPoint(box.center),
                    axisX,
                    axisZ,
                    halfX,
                    halfZ,
                    halfHeight);
            }

            Bounds bounds = collider.bounds;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            if (radius <= GeometryTolerance)
            {
                throw new InvalidOperationException(
                    $"Traffic collider '{collider.name}' has a degenerate footprint.");
            }
            return PlanarFootprint.Circle(
                bounds.center,
                radius,
                Mathf.Max(bounds.extents.y, GeometryTolerance));
        }

        private static Vector2 Horizontal(Vector3 value) =>
            new(value.x, value.z);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private readonly struct PlanarFootprint
        {
            private PlanarFootprint(Vector3 center, Vector2 axisX,
                Vector2 axisZ, float halfX, float halfZ,
                float halfHeight, bool circle)
            {
                Center = center;
                AxisX = axisX;
                AxisZ = axisZ;
                HalfX = halfX;
                HalfZ = halfZ;
                HalfHeight = halfHeight;
                IsCircle = circle;
            }

            public Vector3 Center { get; }
            private Vector2 AxisX { get; }
            private Vector2 AxisZ { get; }
            private float HalfX { get; }
            private float HalfZ { get; }
            private float HalfHeight { get; }
            private bool IsCircle { get; }

            public static PlanarFootprint Circle(Vector3 center,
                float radius, float halfHeight) =>
                new(center, Vector2.right, Vector2.up,
                    radius, radius, halfHeight, circle: true);

            public static PlanarFootprint Box(Vector3 center,
                Vector2 axisX, Vector2 axisZ, float halfX,
                float halfZ, float halfHeight) =>
                new(center, axisX, axisZ, halfX, halfZ,
                    halfHeight, circle: false);

            public PlanarFootprint Transformed(Vector3 pivot,
                Vector3 offset, Quaternion rotation)
            {
                Vector3 transformedCenter = pivot + offset +
                    rotation * (Center - pivot);
                Vector3 transformedAxisX = rotation *
                    new Vector3(AxisX.x, 0f, AxisX.y);
                Vector3 transformedAxisZ = rotation *
                    new Vector3(AxisZ.x, 0f, AxisZ.y);
                return new PlanarFootprint(
                    transformedCenter,
                    Horizontal(transformedAxisX).normalized,
                    Horizontal(transformedAxisZ).normalized,
                    HalfX,
                    HalfZ,
                    HalfHeight,
                    IsCircle);
            }

            public float Bottom => Center.y - HalfHeight;

            public Vector3 ResolveWorldQueryHalfExtents(float clearance)
            {
                float worldHalfX = IsCircle
                    ? HalfX
                    : Mathf.Abs(AxisX.x) * HalfX +
                      Mathf.Abs(AxisZ.x) * HalfZ;
                float worldHalfZ = IsCircle
                    ? HalfX
                    : Mathf.Abs(AxisX.y) * HalfX +
                      Mathf.Abs(AxisZ.y) * HalfZ;
                return new Vector3(
                    worldHalfX + clearance,
                    Mathf.Max(HalfHeight, GeometryTolerance),
                    worldHalfZ + clearance);
            }

            public float ProjectedRadius(Vector2 axis)
            {
                if (IsCircle)
                    return HalfX;

                return HalfX * Mathf.Abs(Vector2.Dot(AxisX, axis)) +
                       HalfZ * Mathf.Abs(Vector2.Dot(AxisZ, axis));
            }

            public bool Overlaps(PlanarFootprint other, float clearance)
            {
                if (Mathf.Abs(Center.y - other.Center.y) >
                    HalfHeight + other.HalfHeight + clearance)
                {
                    return false;
                }
                if (IsCircle && other.IsCircle)
                {
                    float radius = HalfX + other.HalfX + clearance;
                    return (Horizontal(Center) - Horizontal(other.Center))
                        .sqrMagnitude <= radius * radius;
                }
                if (IsCircle)
                    return CircleOverlapsBox(this, other, clearance);
                if (other.IsCircle)
                    return CircleOverlapsBox(other, this, clearance);

                return BoxOverlapsBox(this, other, clearance);
            }

            private static bool CircleOverlapsBox(PlanarFootprint circle,
                PlanarFootprint box, float clearance)
            {
                Vector2 offset = Horizontal(circle.Center - box.Center);
                float localX = Vector2.Dot(offset, box.AxisX);
                float localZ = Vector2.Dot(offset, box.AxisZ);
                float closestX = Mathf.Clamp(localX, -box.HalfX, box.HalfX);
                float closestZ = Mathf.Clamp(localZ, -box.HalfZ, box.HalfZ);
                Vector2 closest = box.AxisX * closestX +
                                  box.AxisZ * closestZ;
                float radius = circle.HalfX + clearance;
                return (offset - closest).sqrMagnitude <= radius * radius;
            }

            private static bool BoxOverlapsBox(PlanarFootprint first,
                PlanarFootprint second, float clearance)
            {
                Vector2 centerOffset = Horizontal(second.Center - first.Center);
                return OverlapsOnAxis(first, second, centerOffset,
                           first.AxisX, clearance) &&
                       OverlapsOnAxis(first, second, centerOffset,
                           first.AxisZ, clearance) &&
                       OverlapsOnAxis(first, second, centerOffset,
                           second.AxisX, clearance) &&
                       OverlapsOnAxis(first, second, centerOffset,
                           second.AxisZ, clearance);
            }

            private static bool OverlapsOnAxis(PlanarFootprint first,
                PlanarFootprint second, Vector2 centerOffset,
                Vector2 axis, float clearance)
            {
                float centerDistance = Mathf.Abs(Vector2.Dot(centerOffset, axis));
                float firstRadius = first.HalfX *
                                    Mathf.Abs(Vector2.Dot(first.AxisX, axis)) +
                                    first.HalfZ *
                                    Mathf.Abs(Vector2.Dot(first.AxisZ, axis));
                float secondRadius = second.HalfX *
                                     Mathf.Abs(Vector2.Dot(second.AxisX, axis)) +
                                     second.HalfZ *
                                     Mathf.Abs(Vector2.Dot(second.AxisZ, axis));
                return centerDistance <= firstRadius + secondRadius + clearance;
            }
        }
    }
}
