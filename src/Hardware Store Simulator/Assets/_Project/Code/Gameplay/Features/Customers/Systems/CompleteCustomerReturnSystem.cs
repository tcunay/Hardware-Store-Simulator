using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class CompleteCustomerReturnSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly CustomerVehicleConfig _vehicleConfig;
        private readonly IGroup<GameEntity> _customers;
        private readonly List<GameEntity> _buffer = new(4);

        public CompleteCustomerReturnSystem(GameContext gameContext,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _vehicleConfig = staticData.CustomerVehicle;
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
            if (!visit.isCustomerVisit || !visit.isCustomerVehicle ||
                !visit.isRouteMover || !visit.isCustomerVisitReturning ||
                !visit.isOrder || !visit.isOrderRewarded || !visit.hasEntityId ||
                !visit.hasDepartureRoute || !visit.hasMovementSpeed ||
                visit.isInteractable || visit.hasCustomerDepartureDelayRemaining ||
                visit.hasRoute || visit.hasRouteWaypointIndex || visit.isRouteCompleted ||
                visit.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Customer {customer.EntityId} cannot complete its return for visit " +
                    $"{visit.EntityId}.");
            }

            Pose[] departureRoute = visit.DepartureRoute;
            if (departureRoute == null || departureRoute.Length < 2)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid departure route.");

            customer.isRouteCompleted = false;
            customer.RemoveRoute();
            customer.RemoveRouteWaypointIndex();
            customer.isCustomerReturningToVehicle = false;
            customer.RemoveCustomerActorVisitEntityId();
            customer.isDestructed = true;

            visit.isCustomerVisitReturning = false;
            visit.isCustomerVisitDeparting = true;
            visit.AddRoute((Pose[])departureRoute.Clone());
            visit.AddRouteWaypointIndex(1);
            visit.ReplaceMovementSpeed(_vehicleConfig.DepartureSpeed);
            visit.RemoveDepartureRoute();
        }
    }
}
