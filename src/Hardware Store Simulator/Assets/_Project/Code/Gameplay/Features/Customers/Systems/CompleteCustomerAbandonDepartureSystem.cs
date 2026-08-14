using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class CompleteCustomerAbandonDepartureSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public CompleteCustomerAbandonDepartureSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.CustomerVisitAbandonDeparting,
                    GameMatcher.EntityId,
                    GameMatcher.CustomerVisitStoreEntityId,
                    GameMatcher.ReservedCustomerParkingSpotEntityId,
                    GameMatcher.ReservedCustomerTrafficLaneEntityId,
                    GameMatcher.Route,
                    GameMatcher.RouteWaypointIndex,
                    GameMatcher.RouteCompleted)
                .NoneOf(
                    GameMatcher.Order,
                    GameMatcher.ReservedCustomerLoadingBayEntityId,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
                CompleteDeparture(visit);
        }

        private void CompleteDeparture(GameEntity visit)
        {
            if (_gameContext.GetEntityWithCustomerActorVisitEntityId(visit.EntityId) != null ||
                _gameContext.GetEntitiesWithConsultationOfferVisitEntityId(
                    visit.EntityId).Count != 0 ||
                visit.hasServingOrderCounterEntityId ||
                visit.hasCustomerPatienceRemaining || visit.isInteractable)
            {
                throw new InvalidOperationException(
                    $"Abandoned customer visit {visit.EntityId} retains pre-order state.");
            }

            GameEntity store = _gameContext.GetEntityWithEntityId(
                visit.CustomerVisitStoreEntityId);
            if (store == null || store.isDestructed || !store.isStore ||
                !store.hasEntityId || (!store.isStoreOpen && !store.isStoreClosing) ||
                store.isStoreOpen == store.isStoreClosing)
            {
                throw new InvalidOperationException(
                    $"Abandoned customer visit {visit.EntityId} references an invalid store.");
            }

            GameEntity parkingSpot = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerParkingSpotEntityId);
            if (parkingSpot == null || parkingSpot.isDestructed ||
                !parkingSpot.isCustomerParkingSpot ||
                _gameContext.GetEntityWithReservedCustomerParkingSpotEntityId(
                    parkingSpot.EntityId) != visit)
            {
                throw new InvalidOperationException(
                    $"Abandoned customer visit {visit.EntityId} has an invalid parking spot.");
            }

            GameEntity trafficLane = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerTrafficLaneEntityId);
            if (trafficLane == null || trafficLane.isDestructed ||
                !trafficLane.isCustomerTrafficLane ||
                _gameContext.GetEntityWithReservedCustomerTrafficLaneEntityId(
                    trafficLane.EntityId) != visit)
            {
                throw new InvalidOperationException(
                    $"Abandoned customer visit {visit.EntityId} has an invalid traffic lane.");
            }

            visit.RemoveCustomerVisitStoreEntityId();
            visit.RemoveReservedCustomerParkingSpotEntityId();
            visit.RemoveReservedCustomerTrafficLaneEntityId();
            visit.isDestructed = true;
        }
    }
}
