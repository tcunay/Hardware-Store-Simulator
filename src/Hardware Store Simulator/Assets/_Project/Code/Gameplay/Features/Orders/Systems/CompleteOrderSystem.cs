using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Orders.Systems
{
    public sealed class CompleteOrderSystem : IExecuteSystem
    {
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _orders;
        private readonly List<GameEntity> _buffer = new(4);

        public CompleteOrderSystem(GameContext gameContext, IGameEventFactory events)
        {
            _events = events;
            _orders = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.Order,
                GameMatcher.OrderActive,
                GameMatcher.LoadedProductCount,
                GameMatcher.RequiredProductCount,
                GameMatcher.OrderReward));
        }

        public void Execute()
        {
            foreach (GameEntity order in _orders.GetEntities(_buffer))
            {
                if (order.LoadedProductCount < order.RequiredProductCount)
                    continue;

                order.isOrderActive = false;
                order.isOrderCompleted = true;
                _events.EmitOrderCompleted(order.EntityId);
                _events.EmitNotification($"Заказ выполнен: +{order.OrderReward:N0} ₽");
            }
        }
    }
}
