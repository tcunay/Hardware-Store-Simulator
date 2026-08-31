using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class MoveCustomerVehicleToLoadingBaySystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly CustomerVehicleConfig _config;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public MoveCustomerVehicleToLoadingBaySystem(GameContext gameContext,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _config = staticData.CustomerVehicle;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.Order,
                    GameMatcher.CustomerVisitWaitingForLoadingBay,
                    GameMatcher.EntityId,
                    GameMatcher.CustomerVisitStoreEntityId,
                    GameMatcher.ReservedCustomerParkingSpotEntityId,
                    GameMatcher.ReservedCustomerLoadingBayEntityId,
                    GameMatcher.MovementSpeed)
                .NoneOf(
                    GameMatcher.ReservedCustomerTrafficLaneEntityId,
                    GameMatcher.Route,
                    GameMatcher.RouteWaypointIndex,
                    GameMatcher.RouteCompleted,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
                TryBeginMove(visit);
        }

        private void TryBeginMove(GameEntity visit)
        {
            GameEntity trafficLane =
                _gameContext.GetEntityWithCustomerTrafficLaneStoreEntityId(
                    visit.CustomerVisitStoreEntityId);
            ValidateTrafficLane(visit, trafficLane);
            if (_gameContext.GetEntityWithReservedCustomerTrafficLaneEntityId(
                    trafficLane.EntityId) != null)
                return;

            GameEntity parkingSpot = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerParkingSpotEntityId);
            if (parkingSpot == null || parkingSpot.isDestructed ||
                !parkingSpot.isCustomerParkingSpot ||
                !parkingSpot.hasCustomerVehicleToLoadingRoute ||
                parkingSpot.CustomerVehicleToLoadingRoute == null ||
                parkingSpot.CustomerVehicleToLoadingRoute.Length < 2)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid loading route.");
            }
            GameEntity bay = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerLoadingBayEntityId);
            if (bay == null || bay.isDestructed || !bay.isCustomerLoadingBay ||
                !bay.hasCustomerLoadingBayStoreEntityId ||
                bay.CustomerLoadingBayStoreEntityId != visit.CustomerVisitStoreEntityId)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid loading-bay reservation.");
            }

            Pose[] route = (Pose[])parkingSpot.CustomerVehicleToLoadingRoute.Clone();
            visit.AddReservedCustomerTrafficLaneEntityId(trafficLane.EntityId);
            visit.AddRoute(route);
            visit.AddRouteWaypointIndex(1);
            visit.ReplaceMovementSpeed(_config.ArrivalSpeed);
            visit.ReplaceTrafficCurrentSpeed(0f);
            visit.isCustomerVisitWaitingForLoadingBay = false;
            visit.isCustomerVisitMovingToLoadingBay = true;
        }

        private static void ValidateTrafficLane(GameEntity visit, GameEntity trafficLane)
        {
            if (trafficLane == null || trafficLane.isDestructed ||
                !trafficLane.isCustomerTrafficLane || !trafficLane.hasEntityId ||
                !trafficLane.hasCustomerTrafficLaneStoreEntityId ||
                trafficLane.CustomerTrafficLaneStoreEntityId !=
                visit.CustomerVisitStoreEntityId)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid traffic lane.");
            }
        }
    }
}
