using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Time;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class MoveCustomerVehicleRouteSystem : IExecuteSystem
    {
        private const float WaypointRotationTolerance = 0.1f;

        private readonly ITimeService _time;
        private readonly IGroup<GameEntity> _vehicles;
        private readonly List<GameEntity> _buffer = new(4);

        public MoveCustomerVehicleRouteSystem(GameContext gameContext, ITimeService time)
        {
            _time = time;
            _vehicles = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.EntityId,
                    GameMatcher.Route,
                    GameMatcher.RouteWaypointIndex,
                    GameMatcher.MovementSpeed,
                    GameMatcher.RotationSpeed,
                    GameMatcher.WaypointTolerance,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody)
                .AnyOf(
                    GameMatcher.CustomerVisitArriving,
                    GameMatcher.CustomerVisitDeparting)
                .NoneOf(
                    GameMatcher.RouteCompleted,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity vehicle in _vehicles.GetEntities(_buffer))
                Move(vehicle);
        }

        private void Move(GameEntity vehicle)
        {
            if (vehicle.isCustomerVisitArriving == vehicle.isCustomerVisitDeparting)
                throw new InvalidOperationException(
                    $"Moving customer vehicle {vehicle.EntityId} must have exactly one route state.");

            Pose[] route = vehicle.Route;
            int waypointIndex = vehicle.RouteWaypointIndex;
            if (route == null || route.Length < 2)
                throw new InvalidOperationException(
                    $"Customer vehicle {vehicle.EntityId} has an invalid route.");
            if (waypointIndex <= 0 || waypointIndex >= route.Length)
                throw new InvalidOperationException(
                    $"Customer vehicle {vehicle.EntityId} has invalid waypoint index {waypointIndex}.");

            Rigidbody body = vehicle.Rigidbody;
            if (!body.isKinematic)
                throw new InvalidOperationException(
                    $"Customer vehicle {vehicle.EntityId} requires a kinematic rigidbody.");
            if (body.interpolation != RigidbodyInterpolation.None)
                throw new InvalidOperationException(
                    $"Customer vehicle {vehicle.EntityId} requires rigidbody interpolation None.");

            Pose target = route[waypointIndex];
            float deltaTime = _time.DeltaTime;
            Vector3 position = Vector3.MoveTowards(
                body.position,
                target.position,
                vehicle.MovementSpeed * deltaTime);
            Quaternion rotation = Quaternion.RotateTowards(
                body.rotation,
                target.rotation,
                vehicle.RotationSpeed * deltaTime);

            bool positionReached =
                (position - target.position).sqrMagnitude <=
                vehicle.WaypointTolerance * vehicle.WaypointTolerance;
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
            vehicle.Transform.SetPositionAndRotation(position, rotation);

            if (!waypointReached)
                return;

            int nextWaypointIndex = waypointIndex + 1;
            if (nextWaypointIndex < route.Length)
                vehicle.ReplaceRouteWaypointIndex(nextWaypointIndex);
            else
                vehicle.isRouteCompleted = true;
        }
    }
}
