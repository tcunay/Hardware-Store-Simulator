using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Physics;
using HardwareStore.Gameplay.Common.Time;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class MoveRouteSystem : IExecuteSystem
    {
        private const float WaypointRotationTolerance = 0.1f;

        private readonly IRouteMotionService _motion;
        private readonly ITimeService _time;
        private readonly LocalTrafficConfig _trafficConfig;
        private readonly IGroup<GameEntity> _routeMovers;
        private readonly List<GameEntity> _buffer = new(8);

        public MoveRouteSystem(GameContext gameContext,
            IRouteMotionService motion, ITimeService time,
            IStaticDataService staticData)
        {
            _motion = motion;
            _time = time;
            _trafficConfig = staticData.LocalTraffic;
            _routeMovers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.RouteMover,
                    GameMatcher.EntityId,
                    GameMatcher.Route,
                    GameMatcher.RouteWaypointIndex,
                    GameMatcher.MovementSpeed,
                    GameMatcher.RotationSpeed,
                    GameMatcher.WaypointTolerance,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody,
                    GameMatcher.Colliders,
                    GameMatcher.TrafficCurrentSpeed)
                .NoneOf(
                    GameMatcher.CustomerVehicle,
                    GameMatcher.RouteCompleted,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity routeMover in _routeMovers.GetEntities(_buffer))
                Move(routeMover);
        }

        private void Move(GameEntity routeMover)
        {
            if (routeMover.isCustomer)
            {
                GhostMoverCollisionProfile.Apply(
                    routeMover.Rigidbody,
                    routeMover.Colliders,
                    GhostMoverCollisionProfile.GhostMover);
            }

            Pose[] route = routeMover.Route;
            int waypointIndex = routeMover.RouteWaypointIndex;
            if (route == null || route.Length < 2)
                throw new InvalidOperationException(
                    $"Route mover {routeMover.EntityId} has an invalid route.");
            if (waypointIndex <= 0 || waypointIndex >= route.Length)
                throw new InvalidOperationException(
                    $"Route mover {routeMover.EntityId} has invalid waypoint index " +
                    $"{waypointIndex}.");

            Rigidbody body = routeMover.Rigidbody;
            if (!body.isKinematic)
                throw new InvalidOperationException(
                    $"Route mover {routeMover.EntityId} requires a kinematic rigidbody.");
            if (body.interpolation != RigidbodyInterpolation.None)
                throw new InvalidOperationException(
                    $"Route mover {routeMover.EntityId} requires rigidbody interpolation None.");

            Pose target = route[waypointIndex];
            float deltaTime = _time.DeltaTime;
            float movementSpeed = ResolveMovementSpeed(routeMover, deltaTime);
            if (movementSpeed <= 0f)
                return;
            Vector3 position = Vector3.MoveTowards(
                body.position,
                target.position,
                movementSpeed * deltaTime);
            float rotationScale = movementSpeed / routeMover.MovementSpeed;
            Quaternion rotation = Quaternion.RotateTowards(
                body.rotation,
                target.rotation,
                routeMover.RotationSpeed * rotationScale * deltaTime);

            bool positionReached =
                (position - target.position).sqrMagnitude <=
                routeMover.WaypointTolerance * routeMover.WaypointTolerance;
            bool rotationReached =
                Quaternion.Angle(rotation, target.rotation) <= WaypointRotationTolerance;
            bool waypointReached = positionReached && rotationReached;
            if (waypointReached)
            {
                position = target.position;
                rotation = target.rotation;
            }

            if (!_motion.TryResolveMove(
                    body,
                    routeMover.Colliders,
                    position,
                    rotation,
                    out Pose resolvedPose,
                    out _))
            {
                routeMover.ReplaceTrafficCurrentSpeed(0f);
                return;
            }

            body.position = resolvedPose.position;
            body.rotation = resolvedPose.rotation;
            routeMover.Transform.SetPositionAndRotation(
                resolvedPose.position, resolvedPose.rotation);

            if (!waypointReached)
                return;

            int nextWaypointIndex = waypointIndex + 1;
            if (nextWaypointIndex < route.Length)
                routeMover.ReplaceRouteWaypointIndex(nextWaypointIndex);
            else
                routeMover.isRouteCompleted = true;
        }

        private float ResolveMovementSpeed(GameEntity routeMover,
            float deltaTime)
        {
            float current = routeMover.TrafficCurrentSpeed;
            if (float.IsNaN(current) || float.IsInfinity(current) || current < 0f)
            {
                throw new InvalidOperationException(
                    $"Route mover {routeMover.EntityId} has invalid traffic speed {current}.");
            }
            float target = routeMover.isTrafficYielding
                ? 0f
                : routeMover.MovementSpeed;
            float acceleration = target < current
                ? _trafficConfig.VehicleBraking
                : _trafficConfig.VehicleAcceleration;
            float resolved = Mathf.MoveTowards(
                current, target, acceleration * deltaTime);
            routeMover.ReplaceTrafficCurrentSpeed(resolved);
            return resolved;
        }
    }
}
