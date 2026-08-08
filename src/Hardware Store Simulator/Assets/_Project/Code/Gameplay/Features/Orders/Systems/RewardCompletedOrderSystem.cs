using System;
using Entitas;
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
                GameEntity order = _gameContext.GetEntityWithEntityId(completedEvent.OrderEntityId);
                if (!order.isOrder || !order.isOrderCompleted)
                    throw new InvalidOperationException("Only a completed order can be rewarded.");

                GameEntity wallet = _gameContext.GetEntityWithEntityId(order.WalletEntityId);
                if (!wallet.isWallet)
                    throw new InvalidOperationException($"Entity {order.WalletEntityId} is not a wallet.");

                wallet.ReplaceMoney(wallet.Money + order.OrderReward);
                _events.EmitAudio(AudioCueId.Reward);
            }
        }
    }
}
