using System;
using System.Collections.Generic;
using Entitas;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class BeginCustomerReturnSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public BeginCustomerReturnSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.Order,
                    GameMatcher.CustomerVisitReturning,
                    GameMatcher.EntityId,
                    GameMatcher.CustomerVisitStoreEntityId,
                    GameMatcher.ReservedCustomerParkingSpotEntityId)
                .NoneOf(
                    GameMatcher.ServingOrderCounterEntityId,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
                BeginReturn(visit);
        }

        private void BeginReturn(GameEntity visit)
        {
            GameEntity customer =
                _gameContext.GetEntityWithCustomerActorVisitEntityId(visit.EntityId);
            if (customer != null && customer.isCustomerReturningToVehicle &&
                customer.hasRoute && customer.hasRouteWaypointIndex &&
                !customer.isRouteCompleted && !customer.hasCustomerReturnRoute &&
                !customer.hasReservedCustomerQueueSpotEntityId)
                return;

            if (customer == null || !customer.isCustomer || !customer.isRouteMover ||
                !customer.isCustomerWaitingAtCounter || !customer.hasEntityId ||
                !customer.hasCustomerReturnRoute ||
                !customer.hasMovementSpeed || !customer.hasRotationSpeed ||
                !customer.hasWaypointTolerance || customer.hasRoute ||
                customer.hasRouteWaypointIndex || customer.isRouteCompleted ||
                customer.hasReservedCustomerQueueSpotEntityId || customer.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} cannot begin its accepted return.");
            }

            Pose[] returnRoute = customer.CustomerReturnRoute;
            if (returnRoute == null || returnRoute.Length < 2)
                throw new InvalidOperationException(
                    $"Customer {customer.EntityId} has an invalid return route.");

            customer.isCustomerWaitingAtCounter = false;
            customer.isCustomerReturningToVehicle = true;
            customer.AddRoute((Pose[])returnRoute.Clone());
            customer.AddRouteWaypointIndex(1);
            customer.RemoveCustomerReturnRoute();
        }
    }
}
