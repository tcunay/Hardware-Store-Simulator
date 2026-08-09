using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class CompleteCustomerVehicleArrivalSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public CompleteCustomerVehicleArrivalSystem(GameContext gameContext)
        {
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.CustomerVisitArriving,
                    GameMatcher.Order,
                    GameMatcher.EntityId,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.RequiredProductCount,
                    GameMatcher.LoadingZone,
                    GameMatcher.Slots,
                    GameMatcher.Route,
                    GameMatcher.RouteWaypointIndex,
                    GameMatcher.RouteCompleted)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
            {
                if (visit.Slots.Length < visit.RequiredProductCount)
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has {visit.Slots.Length} loading slots, " +
                        $"but its order requires {visit.RequiredProductCount} products.");

                visit.isCustomerVisitArriving = false;
                visit.isRouteCompleted = false;
                visit.RemoveRoute();
                visit.RemoveRouteWaypointIndex();
                visit.isCustomerVisitWaiting = true;
                visit.isInteractable = true;
            }
        }
    }
}
