using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Trolley.Systems
{
    public sealed class RegisterCompletedOrderForProgressionSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _rewardedOrders;
        private readonly List<GameEntity> _buffer = new(4);

        public RegisterCompletedOrderForProgressionSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _rewardedOrders = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.EntityId,
                    GameMatcher.Order,
                    GameMatcher.OrderRewarded,
                    GameMatcher.CustomerVisitStoreEntityId)
                .NoneOf(
                    GameMatcher.OrderProgressionCounted,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity order in _rewardedOrders.GetEntities(_buffer))
            {
                GameEntity store = _gameContext.GetEntityWithEntityId(
                    order.CustomerVisitStoreEntityId);
                if (store == null || !store.isStore || !store.hasCompletedOrderCount)
                {
                    throw new InvalidOperationException(
                        $"Rewarded order {order.EntityId} references an invalid store.");
                }

                store.ReplaceCompletedOrderCount(checked(store.CompletedOrderCount + 1));
                order.isOrderProgressionCounted = true;
            }
        }
    }
}
