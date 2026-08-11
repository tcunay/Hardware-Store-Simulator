using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Orders.Systems
{
    public sealed class CompleteOrderSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public CompleteOrderSystem(GameContext gameContext, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.EntityId,
                    GameMatcher.Order,
                    GameMatcher.CustomerVisitLoading,
                    GameMatcher.OrderReward)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
            {
                int lineCount = 0;
                bool complete = true;
                foreach (GameEntity line in
                         _gameContext.GetEntitiesWithOrderEntityId(visit.EntityId))
                {
                    ValidateOrderLine(visit, line);
                    lineCount++;
                    if (line.LoadedProductCount < line.RequiredProductCount)
                        complete = false;
                }

                if (lineCount == 0)
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has no order lines.");
                if (!complete)
                    continue;

                visit.isCustomerVisitLoading = false;
                visit.isCustomerVisitCompleted = true;
                _events.EmitNotification($"Заказ выполнен: +{visit.OrderReward:N0} ₽");
            }
        }

        private static void ValidateOrderLine(GameEntity visit, GameEntity line)
        {
            if (!line.isOrderLine || line.isDestructed || !line.hasEntityId ||
                !line.hasOrderEntityId || !line.hasProductType ||
                !line.hasRequiredProductCount || !line.hasLoadedProductCount ||
                line.OrderEntityId != visit.EntityId ||
                line.RequiredProductCount <= 0 || line.LoadedProductCount < 0 ||
                line.LoadedProductCount > line.RequiredProductCount)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid order line.");
            }
        }
    }
}
