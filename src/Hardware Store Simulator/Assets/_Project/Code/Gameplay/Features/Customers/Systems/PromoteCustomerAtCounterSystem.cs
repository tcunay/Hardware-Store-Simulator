using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class PromoteCustomerAtCounterSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _customers;
        private readonly List<GameEntity> _buffer = new(4);

        public PromoteCustomerAtCounterSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _customers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Customer,
                    GameMatcher.CustomerWaitingInQueue,
                    GameMatcher.EntityId,
                    GameMatcher.CustomerActorVisitEntityId,
                    GameMatcher.ReservedCustomerQueueSpotEntityId)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity customer in _customers.GetEntities(_buffer))
                TryPromote(customer);
        }

        private void TryPromote(GameEntity customer)
        {
            GameEntity queueSpot = _gameContext.GetEntityWithEntityId(
                customer.ReservedCustomerQueueSpotEntityId);
            if (queueSpot == null || queueSpot.isDestructed ||
                !queueSpot.isCustomerQueueSpot || !queueSpot.hasQueueSpotIndex)
            {
                throw new InvalidOperationException(
                    $"Customer {customer.EntityId} has an invalid queue spot.");
            }
            if (queueSpot.QueueSpotIndex != 0)
                return;

            GameEntity visit = _gameContext.GetEntityWithEntityId(
                customer.CustomerActorVisitEntityId);
            if (visit == null || visit.isDestructed || !visit.isCustomerVisit ||
                !visit.isCustomerVisitQueued || !visit.hasCustomerVisitStoreEntityId)
            {
                throw new InvalidOperationException(
                    $"Customer {customer.EntityId} cannot enter consultation.");
            }
            GameEntity store = _gameContext.GetEntityWithEntityId(
                visit.CustomerVisitStoreEntityId);
            if (store == null || store.isDestructed || !store.isStore ||
                !store.hasOrderCounterEntityId)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} references an invalid store.");
            }
            if (_gameContext.GetEntityWithServingOrderCounterEntityId(
                    store.OrderCounterEntityId) != null)
            {
                return;
            }

            visit.isCustomerVisitQueued = false;
            visit.isCustomerVisitConsulting = true;
            visit.AddServingOrderCounterEntityId(store.OrderCounterEntityId);
            customer.isCustomerWaitingInQueue = false;
            customer.isCustomerWaitingAtCounter = true;
        }
    }
}
