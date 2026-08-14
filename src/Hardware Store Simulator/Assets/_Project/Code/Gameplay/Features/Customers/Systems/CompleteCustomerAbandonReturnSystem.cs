using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class CompleteCustomerAbandonReturnSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _customers;
        private readonly List<GameEntity> _buffer = new(4);

        public CompleteCustomerAbandonReturnSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _customers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Customer,
                    GameMatcher.RouteMover,
                    GameMatcher.CustomerAbandonReturningToVehicle,
                    GameMatcher.EntityId,
                    GameMatcher.CustomerActorVisitEntityId,
                    GameMatcher.Route,
                    GameMatcher.RouteWaypointIndex,
                    GameMatcher.RouteCompleted)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity customer in _customers.GetEntities(_buffer))
                CompleteReturn(customer);
        }

        private void CompleteReturn(GameEntity customer)
        {
            GameEntity visit = _gameContext.GetEntityWithEntityId(
                customer.CustomerActorVisitEntityId);
            if (visit == null || visit.isDestructed || !visit.isCustomerVisit ||
                !visit.isCustomerVehicle || !visit.isRouteMover ||
                !visit.isCustomerVisitAbandoning || visit.isOrder ||
                !visit.hasEntityId || !visit.hasCustomerVisitStoreEntityId ||
                !visit.hasReservedCustomerParkingSpotEntityId ||
                visit.hasReservedCustomerTrafficLaneEntityId ||
                visit.hasReservedCustomerLoadingBayEntityId ||
                visit.hasServingOrderCounterEntityId ||
                visit.hasCustomerPatienceRemaining || visit.isInteractable ||
                visit.hasRoute || visit.hasRouteWaypointIndex || visit.isRouteCompleted ||
                customer.hasReservedCustomerQueueSpotEntityId ||
                customer.hasCustomerReturnRoute ||
                _gameContext.GetEntityWithCustomerActorVisitEntityId(
                    visit.EntityId) != customer)
            {
                throw new InvalidOperationException(
                    $"Customer {customer.EntityId} cannot complete its abandoned return.");
            }

            customer.isRouteCompleted = false;
            customer.RemoveRoute();
            customer.RemoveRouteWaypointIndex();
            customer.isCustomerAbandonReturningToVehicle = false;
            customer.RemoveCustomerActorVisitEntityId();
            customer.isDestructed = true;

            visit.isCustomerVisitAbandoning = false;
            visit.isCustomerVisitWaitingForAbandonDeparture = true;
        }
    }
}
