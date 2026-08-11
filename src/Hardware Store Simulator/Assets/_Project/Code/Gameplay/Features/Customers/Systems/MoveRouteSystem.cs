using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Time;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class MoveRouteSystem : IExecuteSystem
    {
        private const float WaypointRotationTolerance = 0.1f;

        private readonly ITimeService _time;
        private readonly IGroup<GameEntity> _routeMovers;
        private readonly List<GameEntity> _buffer = new(8);

        public MoveRouteSystem(GameContext gameContext, ITimeService time)
        {
            _time = time;
            _routeMovers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.RouteMover,
                    GameMatcher.EntityId,
                    GameMatcher.Route,
                    GameMatcher.RouteWaypointIndex,
                    GameMatcher.MovementSpeed,
                    GameMatcher.RotationSpeed,
                    GameMatcher.WaypointTolerance,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody)
                .NoneOf(
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
            Vector3 position = Vector3.MoveTowards(
                body.position,
                target.position,
                routeMover.MovementSpeed * deltaTime);
            Quaternion rotation = Quaternion.RotateTowards(
                body.rotation,
                target.rotation,
                routeMover.RotationSpeed * deltaTime);

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

            body.position = position;
            body.rotation = rotation;
            routeMover.Transform.SetPositionAndRotation(position, rotation);

            if (!waypointReached)
                return;

            int nextWaypointIndex = waypointIndex + 1;
            if (nextWaypointIndex < route.Length)
                routeMover.ReplaceRouteWaypointIndex(nextWaypointIndex);
            else
                routeMover.isRouteCompleted = true;
        }
    }
}
