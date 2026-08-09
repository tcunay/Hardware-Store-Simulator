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
        private readonly CustomerVehicleConfig _config;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public BeginCustomerVehicleDepartureSystem(GameContext gameContext,
            IStaticDataService staticData)
        {
            _config = staticData.CustomerVehicle;
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
                    GameMatcher.MovementSpeed,
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
                    continue;

                Pose[] departureRoute = visit.DepartureRoute;
                if (departureRoute == null || departureRoute.Length < 2)
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has an invalid departure route.");

                visit.isCustomerVisitCompleted = false;
                visit.isInteractable = false;
                visit.isCustomerVisitDeparting = true;
                visit.RemoveCustomerDepartureDelayRemaining();
                visit.AddRoute((Pose[])departureRoute.Clone());
                visit.AddRouteWaypointIndex(1);
                visit.ReplaceMovementSpeed(_config.DepartureSpeed);
                visit.RemoveDepartureRoute();
            }
        }
    }
}
