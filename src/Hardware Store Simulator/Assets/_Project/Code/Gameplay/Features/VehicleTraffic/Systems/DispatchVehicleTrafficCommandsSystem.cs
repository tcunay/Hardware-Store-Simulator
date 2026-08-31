using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.VehicleTraffic;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.VehicleTraffic.Systems
{
    public sealed class DispatchVehicleTrafficCommandsSystem : IExecuteSystem
    {
        private readonly IVehicleTrafficService _traffic;
        private readonly IGroup<GameEntity> _vehicles;
        private readonly List<GameEntity> _buffer = new(8);

        public DispatchVehicleTrafficCommandsSystem(GameContext gameContext,
            IVehicleTrafficService traffic)
        {
            _traffic = traffic;
            _vehicles = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.VehicleTrafficControlled,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.EntityId,
                    GameMatcher.VehicleTrafficCommandSequence,
                    GameMatcher.Route,
                    GameMatcher.RouteWaypointIndex)
                .NoneOf(
                    GameMatcher.RouteCompleted,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity vehicle in _vehicles.GetEntities(_buffer))
                Dispatch(vehicle);
        }

        private void Dispatch(GameEntity vehicle)
        {
            Pose[] route = vehicle.Route;
            if (route == null || route.Length < 2)
            {
                throw new InvalidOperationException(
                    $"Traffic-controlled vehicle {vehicle.EntityId} has an invalid route.");
            }

            if (!vehicle.hasVehicleTrafficRuntimeId)
            {
                if (vehicle.isVehicleTrafficSpawnPending)
                    return;
                if (vehicle.isVehicleTrafficReady || vehicle.isVehicleTrafficMoving ||
                    vehicle.hasView)
                {
                    throw new InvalidOperationException(
                        $"Traffic-controlled vehicle {vehicle.EntityId} has an invalid " +
                        "pre-spawn state.");
                }

                int commandSequence = NextCommand(vehicle);
                vehicle.isVehicleTrafficSpawnPending = true;
                _traffic.RequestSpawn(vehicle.EntityId, commandSequence,
                    route[0], route[^1]);
                return;
            }

            if (!vehicle.isVehicleTrafficReady || vehicle.isVehicleTrafficSpawnPending)
            {
                throw new InvalidOperationException(
                    $"Traffic-controlled vehicle {vehicle.EntityId} has a runtime id " +
                    "without a ready provider view.");
            }
            if (vehicle.isVehicleTrafficMoving)
                return;

            int moveSequence = NextCommand(vehicle);
            vehicle.isVehicleTrafficMoving = true;
            _traffic.RequestDestination(
                vehicle.VehicleTrafficRuntimeId,
                vehicle.EntityId,
                moveSequence,
                route[^1]);
        }

        private static int NextCommand(GameEntity vehicle)
        {
            int next = checked(vehicle.VehicleTrafficCommandSequence + 1);
            vehicle.ReplaceVehicleTrafficCommandSequence(next);
            return next;
        }
    }
}
