using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class CompleteCustomerReturnSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _customers;
        private readonly List<GameEntity> _buffer = new(4);

        public CompleteCustomerReturnSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _customers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Customer,
                    GameMatcher.RouteMover,
                    GameMatcher.CustomerReturningToVehicle,
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
            if (visit == null || !visit.isCustomerVisit || !visit.isCustomerVehicle ||
                !visit.isRouteMover || !visit.isCustomerVisitReturning ||
                !visit.isOrder || !visit.hasEntityId ||
                !visit.hasReservedCustomerParkingSpotEntityId ||
                visit.isInteractable || visit.hasRoute ||
                visit.hasRouteWaypointIndex || visit.isRouteCompleted ||
                visit.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Customer {customer.EntityId} cannot complete its return to vehicle.");
            }

            customer.isRouteCompleted = false;
            customer.RemoveRoute();
            customer.RemoveRouteWaypointIndex();
            customer.isCustomerReturningToVehicle = false;
            customer.RemoveCustomerActorVisitEntityId();
            customer.isDestructed = true;

            visit.isCustomerVisitReturning = false;
            visit.isCustomerVisitWaitingForLoadingBay = true;
        }
    }
}
