using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class BeginCustomerVehicleDepartureSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly CustomerVehicleConfig _config;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public BeginCustomerVehicleDepartureSystem(GameContext gameContext,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _config = staticData.CustomerVehicle;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.Order,
                    GameMatcher.CustomerVisitCompleted,
                    GameMatcher.OrderRewarded,
                    GameMatcher.EntityId,
                    GameMatcher.CustomerVisitStoreEntityId,
                    GameMatcher.ReservedCustomerLoadingBayEntityId,
                    GameMatcher.CustomerDepartureDelayRemaining,
                    GameMatcher.MovementSpeed)
                .NoneOf(
                    GameMatcher.ReservedCustomerParkingSpotEntityId,
                    GameMatcher.ReservedCustomerTrafficLaneEntityId,
                    GameMatcher.Route,
                    GameMatcher.RouteWaypointIndex,
                    GameMatcher.RouteCompleted,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
                TryBeginDeparture(visit);
        }

        private void TryBeginDeparture(GameEntity visit)
        {
            float delay = visit.CustomerDepartureDelayRemaining;
            if (float.IsNaN(delay) || float.IsInfinity(delay) || delay < 0f)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has invalid departure delay {delay}.");
            if (delay > 0f)
                return;
            if (_gameContext.GetEntityWithCustomerActorVisitEntityId(visit.EntityId) != null)
                throw new InvalidOperationException(
                    $"Completed customer visit {visit.EntityId} still has a customer actor.");

            GameEntity trafficLane =
                _gameContext.GetEntityWithCustomerTrafficLaneStoreEntityId(
                    visit.CustomerVisitStoreEntityId);
            if (trafficLane == null || trafficLane.isDestructed ||
                !trafficLane.isCustomerTrafficLane || !trafficLane.hasEntityId ||
                !trafficLane.hasCustomerTrafficLaneStoreEntityId ||
                trafficLane.CustomerTrafficLaneStoreEntityId !=
                visit.CustomerVisitStoreEntityId)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid traffic lane.");
            }
            if (_gameContext.GetEntityWithReservedCustomerTrafficLaneEntityId(
                    trafficLane.EntityId) != null)
                return;

            GameEntity bay = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerLoadingBayEntityId);
            if (bay == null || bay.isDestructed || !bay.isCustomerLoadingBay ||
                !bay.hasCustomerLoadingDepartureRoute ||
                bay.CustomerLoadingDepartureRoute == null ||
                bay.CustomerLoadingDepartureRoute.Length < 2)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid departure route.");
            }

            Pose[] route = (Pose[])bay.CustomerLoadingDepartureRoute.Clone();
            visit.AddReservedCustomerTrafficLaneEntityId(trafficLane.EntityId);
            visit.AddRoute(route);
            visit.AddRouteWaypointIndex(1);
            visit.ReplaceMovementSpeed(_config.DepartureSpeed);
            visit.ReplaceTrafficCurrentSpeed(0f);
            visit.RemoveCustomerDepartureDelayRemaining();
            visit.isCustomerVisitCompleted = false;
            visit.isCustomerVisitDeparting = true;
            visit.isInteractable = false;
        }
    }
}
