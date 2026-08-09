using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Orders.Systems
{
    public sealed class CompleteOrderSystem : IExecuteSystem
    {
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public CompleteOrderSystem(GameContext gameContext, IGameEventFactory events)
        {
            _events = events;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.Order,
                    GameMatcher.CustomerVisitLoading,
                    GameMatcher.LoadedProductCount,
                    GameMatcher.RequiredProductCount,
                    GameMatcher.OrderReward)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
            {
                if (visit.LoadedProductCount < visit.RequiredProductCount)
                    continue;

                visit.isCustomerVisitLoading = false;
                visit.isCustomerVisitCompleted = true;
                _events.EmitNotification($"Заказ выполнен: +{visit.OrderReward:N0} ₽");
            }
        }
    }
}
