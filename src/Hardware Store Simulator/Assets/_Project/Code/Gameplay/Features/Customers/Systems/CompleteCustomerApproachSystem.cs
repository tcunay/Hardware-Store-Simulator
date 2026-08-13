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
                    GameMatcher.ReservedCustomerQueueSpotEntityId,
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
            if (visit == null || !visit.isCustomerVisit || !visit.isCustomerVehicle ||
                !visit.isCustomerVisitQueued || visit.isInteractable ||
                visit.hasRoute || visit.hasRouteWaypointIndex || visit.isRouteCompleted ||
                visit.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Customer {customer.EntityId} cannot complete its queue approach.");
            }

            GameEntity queueSpot = _gameContext.GetEntityWithEntityId(
                customer.ReservedCustomerQueueSpotEntityId);
            if (queueSpot == null || queueSpot.isDestructed ||
                !queueSpot.isCustomerQueueSpot || !queueSpot.hasQueueSpotIndex)
            {
                throw new InvalidOperationException(
                    $"Customer {customer.EntityId} has an invalid queue reservation.");
            }

            customer.isRouteCompleted = false;
            customer.RemoveRoute();
            customer.RemoveRouteWaypointIndex();
            customer.isCustomerApproachingCounter = false;
            customer.isCustomerWaitingInQueue = true;
        }
    }
}
