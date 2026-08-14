using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;

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
                if (ValidateCompletionReservations(visit))
                    continue;

                visit.isCustomerVisitLoading = false;
                visit.isCustomerVisitCompleted = true;
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationOrderCompleted,
                    visit.OrderReward));
            }
        }

        private bool ValidateCompletionReservations(GameEntity visit)
        {
            foreach (GameEntity line in
                     _gameContext.GetEntitiesWithOrderEntityId(visit.EntityId))
            {
                foreach (GameEntity product in
                         _gameContext.GetEntitiesWithReservedOrderLineEntityId(
                             line.EntityId))
                {
                    if (!product.isDestructed)
                    {
                        throw new InvalidOperationException(
                            $"Completed order line {line.EntityId} still reserves product " +
                            $"{product.EntityId}.");
                    }
                }
            }

            bool blockedTaskPendingCleanup = false;
            foreach (GameEntity task in
                     _gameContext.GetEntitiesWithWarehouseTaskCustomerVisitEntityId(
                         visit.EntityId))
            {
                if (task.isDestructed)
                    continue;
                if (!task.isWarehouseTask ||
                    !task.isStockToCustomerLoadingTask ||
                    task.isInboundToStorageTask || !task.hasEntityId ||
                    !task.hasWarehouseTaskOrderLineEntityId ||
                    !task.hasWarehouseTaskStep ||
                    !task.hasWarehouseTaskBlockReason)
                {
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has invalid outbound task.");
                }
                GameEntity line = _gameContext.GetEntityWithEntityId(
                    task.WarehouseTaskOrderLineEntityId);
                if (line == null || line.isDestructed || !line.isOrderLine ||
                    !line.hasOrderEntityId || line.OrderEntityId != visit.EntityId)
                {
                    throw new InvalidOperationException(
                        $"Outbound task {task.EntityId} has invalid order-line target.");
                }
                if (task.WarehouseTaskStep != WarehouseTaskStepId.Blocked ||
                    task.hasAssignedWorkerEntityId ||
                    task.hasWarehouseTaskReservedLoadingSlotIndex ||
                    task.WarehouseTaskBlockReason ==
                    WarehouseTaskBlockReasonId.None)
                {
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} completed with active outbound " +
                        $"task {task.EntityId}.");
                }
                blockedTaskPendingCleanup = true;
            }

            return blockedTaskPendingCleanup;
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
