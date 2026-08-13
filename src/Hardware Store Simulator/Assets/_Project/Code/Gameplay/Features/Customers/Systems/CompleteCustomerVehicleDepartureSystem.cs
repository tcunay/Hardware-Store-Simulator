using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class CompleteCustomerVehicleDepartureSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public CompleteCustomerVehicleDepartureSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.CustomerVisitDeparting,
                    GameMatcher.Order,
                    GameMatcher.OrderRewarded,
                    GameMatcher.OrderContentReleased,
                    GameMatcher.EntityId,
                    GameMatcher.CustomerVisitStoreEntityId,
                    GameMatcher.ReservedCustomerLoadingBayEntityId,
                    GameMatcher.ReservedCustomerTrafficLaneEntityId,
                    GameMatcher.LoadingZone,
                    GameMatcher.RouteCompleted)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
                CompleteDeparture(visit);
        }

        private void CompleteDeparture(GameEntity visit)
        {
            if (_gameContext.GetEntitiesWithOrderEntityId(visit.EntityId).Count != 0)
            {
                throw new InvalidOperationException(
                    $"Departed customer visit {visit.EntityId} still owns order content.");
            }
            GameEntity store = _gameContext.GetEntityWithEntityId(
                visit.CustomerVisitStoreEntityId);
            if (store == null || store.isDestructed || !store.isStore ||
                !store.hasEntityId || (!store.isStoreOpen && !store.isStoreClosing))
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} references an invalid active store.");
            }
            GameEntity bay = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerLoadingBayEntityId);
            if (bay == null || bay.isDestructed || !bay.isCustomerLoadingBay)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid loading bay.");
            GameEntity trafficLane = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerTrafficLaneEntityId);
            if (trafficLane == null || trafficLane.isDestructed ||
                !trafficLane.isCustomerTrafficLane)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid traffic lane.");
            }

            visit.RemoveCustomerVisitStoreEntityId();
            visit.RemoveReservedCustomerLoadingBayEntityId();
            visit.RemoveReservedCustomerTrafficLaneEntityId();
            visit.isDestructed = true;
        }
    }
}
