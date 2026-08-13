using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class CompleteCustomerLoadingBayArrivalSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public CompleteCustomerLoadingBayArrivalSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.Order,
                    GameMatcher.CustomerVisitMovingToLoadingBay,
                    GameMatcher.EntityId,
                    GameMatcher.ReservedCustomerParkingSpotEntityId,
                    GameMatcher.ReservedCustomerLoadingBayEntityId,
                    GameMatcher.ReservedCustomerTrafficLaneEntityId,
                    GameMatcher.Route,
                    GameMatcher.RouteWaypointIndex,
                    GameMatcher.RouteCompleted)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
                CompleteArrival(visit);
        }

        private void CompleteArrival(GameEntity visit)
        {
            GameEntity parkingSpot = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerParkingSpotEntityId);
            if (parkingSpot == null || parkingSpot.isDestructed ||
                !parkingSpot.isCustomerParkingSpot)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid parking reservation.");
            }
            GameEntity bay = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerLoadingBayEntityId);
            if (bay == null || bay.isDestructed || !bay.isCustomerLoadingBay)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid loading bay.");
            GameEntity trafficLane = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerTrafficLaneEntityId);
            if (trafficLane == null || trafficLane.isDestructed ||
                !trafficLane.isCustomerTrafficLane)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid traffic lane.");
            }

            visit.isRouteCompleted = false;
            visit.RemoveRoute();
            visit.RemoveRouteWaypointIndex();
            visit.RemoveReservedCustomerTrafficLaneEntityId();
            visit.RemoveReservedCustomerParkingSpotEntityId();
            visit.isCustomerVisitMovingToLoadingBay = false;
            visit.isCustomerVisitLoading = true;
            visit.isInteractable = true;
        }
    }
}
