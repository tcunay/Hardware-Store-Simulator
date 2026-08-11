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
                    GameMatcher.CustomerVisitCompleted,
                    GameMatcher.OrderRewarded,
                    GameMatcher.EntityId,
                    GameMatcher.LoadingZone,
                    GameMatcher.Interactable,
                    GameMatcher.DepartureRoute,
                    GameMatcher.CustomerDepartureDelayRemaining)
                .NoneOf(
                    GameMatcher.Route,
                    GameMatcher.RouteWaypointIndex,
                    GameMatcher.RouteCompleted,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
                BeginReturn(visit);
        }

        private void BeginReturn(GameEntity visit)
        {
            float departureDelay = visit.CustomerDepartureDelayRemaining;
            if (float.IsNaN(departureDelay) || float.IsInfinity(departureDelay) ||
                departureDelay < 0f)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has invalid departure delay " +
                    $"{departureDelay}.");
            }
            if (departureDelay > 0f)
                return;

            GameEntity customer =
                _gameContext.GetEntityWithCustomerActorVisitEntityId(visit.EntityId);
            if (customer == null)
                throw new InvalidOperationException(
                    $"Completed customer visit {visit.EntityId} has no customer actor.");
            if (!customer.isCustomer || !customer.isRouteMover ||
                !customer.isCustomerWaitingAtCounter || !customer.hasEntityId ||
                !customer.hasCustomerReturnRoute || !customer.hasMovementSpeed ||
                !customer.hasRotationSpeed || !customer.hasWaypointTolerance ||
                customer.hasRoute || customer.hasRouteWaypointIndex ||
                customer.isRouteCompleted || customer.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Customer {customer.EntityId} cannot return to vehicle for visit " +
                    $"{visit.EntityId}.");
            }

            Pose[] returnRoute = customer.CustomerReturnRoute;
            if (returnRoute == null || returnRoute.Length < 2)
                throw new InvalidOperationException(
                    $"Customer {customer.EntityId} has an invalid return route.");

            visit.isCustomerVisitCompleted = false;
            visit.isCustomerVisitReturning = true;
            visit.isInteractable = false;
            visit.RemoveCustomerDepartureDelayRemaining();

            customer.isCustomerWaitingAtCounter = false;
            customer.isCustomerReturningToVehicle = true;
            customer.AddRoute((Pose[])returnRoute.Clone());
            customer.AddRouteWaypointIndex(1);
            customer.RemoveCustomerReturnRoute();
        }
    }
}
