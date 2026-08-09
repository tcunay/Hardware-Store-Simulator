using System;
using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Orders.Systems
{
    public sealed class RewardCompletedOrderSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _completedEvents;

        public RewardCompletedOrderSystem(GameContext gameContext, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _completedEvents = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.OrderCompletedEvent,
                GameMatcher.OrderEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity completedEvent in _completedEvents)
            {
                GameEntity order = _gameContext.GetRequiredEntity(
                    completedEvent.OrderEntityId,
                    "completed order event target");
                if (!order.isOrder || !order.isOrderCompleted)
                    throw new InvalidOperationException("Only a completed order can be rewarded.");

                GameEntity store = _gameContext.GetRequiredEntity(order.StoreEntityId, "order store");
                if (!store.isStore || !store.hasMoney)
                    throw new InvalidOperationException($"Entity {order.StoreEntityId} is not a configured store.");
                if (!store.hasOrderEntityId || store.OrderEntityId != order.EntityId)
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} does not own completed order {order.EntityId}.");

                store.ReplaceMoney(store.Money + order.OrderReward);
                _events.EmitAudio(AudioCueId.Reward);
            }
        }
    }
}
