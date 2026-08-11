using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class CompleteCustomerApproachSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _customers;
        private readonly List<GameEntity> _buffer = new(4);

        public CompleteCustomerApproachSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _customers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Customer,
                    GameMatcher.RouteMover,
                    GameMatcher.CustomerApproachingCounter,
                    GameMatcher.EntityId,
                    GameMatcher.CustomerActorVisitEntityId,
                    GameMatcher.CustomerReturnRoute,
                    GameMatcher.Route,
                    GameMatcher.RouteWaypointIndex,
                    GameMatcher.RouteCompleted)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity customer in _customers.GetEntities(_buffer))
                CompleteApproach(customer);
        }

        private void CompleteApproach(GameEntity customer)
        {
            GameEntity visit = _gameContext.GetEntityWithEntityId(
                customer.CustomerActorVisitEntityId);
            if (!visit.isCustomerVisit || !visit.isCustomerVehicle ||
                !visit.isCustomerVisitArriving || visit.isInteractable ||
                visit.hasRoute || visit.hasRouteWaypointIndex || visit.isRouteCompleted ||
                visit.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Customer {customer.EntityId} cannot complete its approach for visit " +
                    $"{visit.EntityId}.");
            }

            customer.isRouteCompleted = false;
            customer.RemoveRoute();
            customer.RemoveRouteWaypointIndex();
            customer.isCustomerApproachingCounter = false;
            customer.isCustomerWaitingAtCounter = true;

            visit.isCustomerVisitArriving = false;
            visit.isCustomerVisitConsulting = true;
            visit.isInteractable = true;
        }
    }
}
