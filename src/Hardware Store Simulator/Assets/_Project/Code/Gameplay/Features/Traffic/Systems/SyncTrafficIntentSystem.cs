using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Navigation;
using HardwareStore.Gameplay.Common.Time;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Traffic.Systems
{
    public sealed class SyncTrafficIntentSystem : IExecuteSystem
    {
        private const float MotionTolerance = 0.0001f;
        private const float RotationTolerance = 0.1f;

        private readonly GameContext _gameContext;
        private readonly IWorkerNavigationService _navigation;
        private readonly ITimeService _time;
        private readonly LocalTrafficConfig _config;
        private readonly IGroup<GameEntity> _participants;
        private readonly List<GameEntity> _buffer = new(16);

        public SyncTrafficIntentSystem(GameContext gameContext,
            IWorkerNavigationService navigation, ITimeService time,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _navigation = navigation;
            _time = time;
            _config = staticData.LocalTraffic;
            _participants = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.TrafficParticipant,
                    GameMatcher.EntityId,
                    GameMatcher.TrafficControlPolicy,
                    GameMatcher.TrafficPriority,
                    GameMatcher.TrafficDesiredVelocity,
                    GameMatcher.TrafficIntentDistance,
                    GameMatcher.TrafficAngularIntent,
                    GameMatcher.TrafficPreviousPosition,
                    GameMatcher.Transform)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity participant in
                     _participants.GetEntities(_buffer))
            {
                ResolveIntent(participant, out Vector3 velocity,
                    out float distance);
                float angularIntent = ResolveAngularIntent(participant);
                if (!IsFinite(velocity) || !IsFinite(distance) || distance < 0f ||
                    !IsFinite(angularIntent))
                {
                    throw new InvalidOperationException(
                        $"Traffic participant {participant.EntityId} produced invalid intent.");
                }

                participant.ReplaceTrafficDesiredVelocity(velocity);
                participant.ReplaceTrafficIntentDistance(distance);
                participant.ReplaceTrafficAngularIntent(angularIntent);
                participant.ReplaceTrafficPreviousPosition(
                    participant.Transform.position);
            }
        }

        private void ResolveIntent(GameEntity participant,
            out Vector3 velocity, out float distance)
        {
            if (participant.isRouteMover && participant.hasRoute &&
                participant.hasRouteWaypointIndex &&
                participant.hasMovementSpeed && !participant.isRouteCompleted)
            {
                ResolveRouteIntent(participant, out velocity, out distance);
                return;
            }
            if (participant.hasNavigationAgent)
            {
                WorkerNavigationIntent intent =
                    _navigation.GetPlannedIntent(participant.NavigationAgent);
                velocity = Horizontal(intent.Velocity);
                distance = intent.Distance;
                return;
            }
            if (participant.TrafficControlPolicy ==
                TrafficControlPolicyId.Coupled)
            {
                ResolveCoupledIntent(participant, out velocity, out distance);
                return;
            }
            if (participant.isPlayer)
            {
                if (participant.isDrivingForklift)
                {
                    velocity = Vector3.zero;
                    distance = 0f;
                    return;
                }
                if (!participant.hasMoveDirection || !participant.hasMovementSpeed)
                {
                    throw new InvalidOperationException(
                        $"Traffic player {participant.EntityId} has no movement intent.");
                }

                velocity = Horizontal(participant.MoveDirection) *
                           participant.MovementSpeed;
                distance = velocity.magnitude * _config.PredictionHorizon;
                return;
            }

            ResolveObservedIntent(participant, out velocity, out distance);
        }

        private void ResolveCoupledIntent(GameEntity participant,
            out Vector3 velocity, out float distance)
        {
            if (!participant.hasTrolleyPusherEntityId)
            {
                velocity = Vector3.zero;
                distance = 0f;
                return;
            }

            GameEntity pusher = _gameContext.GetEntityWithEntityId(
                participant.TrolleyPusherEntityId);
            if (pusher == null || pusher.isDestructed ||
                !pusher.isTrafficParticipant || !pusher.hasEntityId ||
                !pusher.hasTransform)
            {
                throw new InvalidOperationException(
                    $"Traffic trolley {participant.EntityId} has an invalid pusher.");
            }

            ResolveIntent(pusher, out velocity, out distance);
        }

        private float ResolveAngularIntent(GameEntity participant)
        {
            if (participant.isPushingTrolley ||
                participant.isPushingWorkerTrolley)
            {
                return ResolveCoupledAngularIntent(participant);
            }
            if (participant.isRouteMover && participant.hasRoute &&
                participant.hasRouteWaypointIndex &&
                participant.hasRotationSpeed && !participant.isRouteCompleted)
            {
                Pose[] route = participant.Route;
                int index = participant.RouteWaypointIndex;
                if (route == null || route.Length < 2 || index <= 0 ||
                    index >= route.Length)
                {
                    throw new InvalidOperationException(
                        $"Traffic route mover {participant.EntityId} has an invalid route.");
                }

                float angle = SignedYawDelta(
                    participant.Transform.rotation, route[index].rotation);
                return Mathf.Abs(angle) <= RotationTolerance ? 0f : angle;
            }
            if (participant.hasNavigationAgent)
            {
                if (!_navigation.UsesAutomaticRotation(
                        participant.NavigationAgent))
                {
                    return 0f;
                }
                WorkerNavigationIntent intent =
                    _navigation.GetPlannedIntent(participant.NavigationAgent);
                Vector3 direction = Horizontal(intent.Velocity);
                if (direction.sqrMagnitude <= MotionTolerance * MotionTolerance)
                    return 0f;

                float angle = SignedYawDelta(
                    participant.Transform.rotation,
                    Quaternion.LookRotation(direction.normalized, Vector3.up));
                return Mathf.Abs(angle) <= RotationTolerance ? 0f : angle;
            }
            if (participant.TrafficControlPolicy !=
                    TrafficControlPolicyId.Coupled ||
                !participant.hasTrolleyPusherEntityId)
            {
                return 0f;
            }

            GameEntity pusher = _gameContext.GetEntityWithEntityId(
                participant.TrolleyPusherEntityId);
            if (pusher == null || pusher.isDestructed ||
                !pusher.isTrafficParticipant || !pusher.hasEntityId ||
                !pusher.hasTransform)
            {
                throw new InvalidOperationException(
                    $"Traffic trolley {participant.EntityId} has an invalid pusher.");
            }

            return ResolveAngularIntent(pusher);
        }

        private float ResolveCoupledAngularIntent(GameEntity pusher)
        {
            GameEntity trolley =
                _gameContext.GetEntityWithTrolleyPusherEntityId(
                    pusher.EntityId);
            if (trolley == null || trolley.isDestructed ||
                !trolley.isTrafficParticipant ||
                trolley.TrafficControlPolicy !=
                TrafficControlPolicyId.Coupled ||
                !trolley.hasTransform)
            {
                throw new InvalidOperationException(
                    $"Traffic pusher {pusher.EntityId} has an invalid coupled trolley.");
            }

            Quaternion target = ResolveOwnerRotationTarget(pusher);
            float angle = SignedYawDelta(
                trolley.Transform.rotation, target);
            return Mathf.Abs(angle) <= RotationTolerance ? 0f : angle;
        }

        private Quaternion ResolveOwnerRotationTarget(GameEntity owner)
        {
            if (owner.isRouteMover && owner.hasRoute &&
                owner.hasRouteWaypointIndex && !owner.isRouteCompleted)
            {
                Pose[] route = owner.Route;
                int index = owner.RouteWaypointIndex;
                if (route == null || route.Length < 2 || index <= 0 ||
                    index >= route.Length)
                {
                    throw new InvalidOperationException(
                        $"Traffic route mover {owner.EntityId} has an invalid route.");
                }

                return route[index].rotation;
            }
            if (owner.hasNavigationAgent &&
                _navigation.UsesAutomaticRotation(owner.NavigationAgent))
            {
                WorkerNavigationIntent intent =
                    _navigation.GetPlannedIntent(owner.NavigationAgent);
                Vector3 direction = Horizontal(intent.Velocity);
                if (direction.sqrMagnitude >
                    MotionTolerance * MotionTolerance)
                {
                    return Quaternion.LookRotation(
                        direction.normalized, Vector3.up);
                }
            }

            return owner.Transform.rotation;
        }

        private static void ResolveRouteIntent(GameEntity participant,
            out Vector3 velocity, out float distance)
        {
            Pose[] route = participant.Route;
            int index = participant.RouteWaypointIndex;
            if (route == null || route.Length < 2 || index <= 0 ||
                index >= route.Length)
            {
                throw new InvalidOperationException(
                    $"Traffic route mover {participant.EntityId} has an invalid route.");
            }

            Vector3 offset = route[index].position - participant.Transform.position;
            offset.y = 0f;
            distance = offset.magnitude;
            velocity = distance <= MotionTolerance
                ? Vector3.zero
                : offset / distance * participant.MovementSpeed;
        }

        private void ResolveObservedIntent(GameEntity participant,
            out Vector3 velocity, out float distance)
        {
            float deltaTime = _time.DeltaTime;
            if (deltaTime <= MotionTolerance)
            {
                velocity = Vector3.zero;
                distance = 0f;
                return;
            }

            velocity = Horizontal(
                participant.Transform.position - participant.TrafficPreviousPosition) /
                deltaTime;
            if (participant.hasRigidbody &&
                participant.Rigidbody.linearVelocity.sqrMagnitude >
                velocity.sqrMagnitude)
            {
                velocity = Horizontal(participant.Rigidbody.linearVelocity);
            }
            distance = velocity.magnitude * _config.PredictionHorizon;
        }

        private static Vector3 Horizontal(Vector3 value) =>
            new(value.x, 0f, value.z);

        private static float SignedYawDelta(Quaternion from, Quaternion to) =>
            Mathf.DeltaAngle(from.eulerAngles.y, to.eulerAngles.y);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
