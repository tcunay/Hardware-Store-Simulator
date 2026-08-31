using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Physics;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class DriveCustomerVehiclePhysicsSystem : IExecuteSystem
    {
        private readonly ICustomerVehiclePhysicsMotor _motor;
        private readonly LocalTrafficConfig _trafficConfig;
        private readonly IGroup<GameEntity> _vehicles;
        private readonly IGroup<GameEntity> _parkedVehicles;
        private readonly List<GameEntity> _buffer = new(8);

        public DriveCustomerVehiclePhysicsSystem(GameContext gameContext,
            ICustomerVehiclePhysicsMotor motor, IStaticDataService staticData)
        {
            _motor = motor;
            _trafficConfig = staticData.LocalTraffic;
            _vehicles = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVehicle,
                    GameMatcher.RouteMover,
                    GameMatcher.EntityId,
                    GameMatcher.Route,
                    GameMatcher.RouteWaypointIndex,
                    GameMatcher.MovementSpeed,
                    GameMatcher.RotationSpeed,
                    GameMatcher.WaypointTolerance,
                    GameMatcher.Rigidbody,
                    GameMatcher.Colliders,
                    GameMatcher.TrafficCurrentSpeed)
                .NoneOf(
                    GameMatcher.RouteCompleted,
                    GameMatcher.Destructed));
            _parkedVehicles = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVehicle,
                    GameMatcher.RouteMover,
                    GameMatcher.Rigidbody)
                .NoneOf(
                    GameMatcher.Route,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity vehicle in _vehicles.GetEntities(_buffer))
                Drive(vehicle);
            foreach (GameEntity vehicle in _parkedVehicles.GetEntities(_buffer))
                _motor.Hold(
                    vehicle.Rigidbody,
                    _trafficConfig.VehicleBraking);
        }

        private void Drive(GameEntity vehicle)
        {
            Pose[] route = vehicle.Route;
            int waypointIndex = vehicle.RouteWaypointIndex;
            if (route == null || route.Length < 2)
                throw new InvalidOperationException(
                    $"Customer vehicle {vehicle.EntityId} has an invalid route.");
            if (waypointIndex <= 0 || waypointIndex >= route.Length)
            {
                throw new InvalidOperationException(
                    $"Customer vehicle {vehicle.EntityId} has invalid waypoint index " +
                    $"{waypointIndex}.");
            }

            CustomerVehiclePhysicsMotorResult result = _motor.Step(
                vehicle.Rigidbody,
                vehicle.Colliders,
                route[waypointIndex - 1],
                route[waypointIndex],
                vehicle.MovementSpeed,
                vehicle.RotationSpeed,
                vehicle.WaypointTolerance,
                _trafficConfig.VehicleAcceleration,
                _trafficConfig.VehicleBraking,
                vehicle.isTrafficYielding);
            vehicle.ReplaceTrafficCurrentSpeed(result.ObservedSpeed);
            if (!result.WaypointReached)
                return;

            int nextWaypointIndex = waypointIndex + 1;
            if (nextWaypointIndex < route.Length)
                vehicle.ReplaceRouteWaypointIndex(nextWaypointIndex);
            else
                vehicle.isRouteCompleted = true;
        }
    }
}
