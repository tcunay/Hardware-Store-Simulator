using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class CleanupBlockedWarehouseTaskSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _tasks;
        private readonly List<GameEntity> _buffer = new(4);

        public CleanupBlockedWarehouseTaskSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _tasks = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseTaskStoreEntityId,
                    GameMatcher.WarehouseTaskProductEntityId,
                    GameMatcher.WarehouseTaskStep,
                    GameMatcher.WarehouseTaskBlockReason)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity task in _tasks.GetEntities(_buffer))
            {
                ValidateRole(task);
                if (task.WarehouseTaskStep != WarehouseTaskStepId.Blocked)
                    continue;
                if (task.WarehouseTaskBlockReason == WarehouseTaskBlockReasonId.None)
                {
                    throw new InvalidOperationException(
                        $"Blocked warehouse task {task.EntityId} has no reason.");
                }
                if (task.hasAssignedWorkerEntityId ||
                    task.hasWarehouseTaskReservedStorageSlotIndex ||
                    task.hasWarehouseTaskReservedLoadingSlotIndex)
                {
                    continue;
                }

                GameEntity product = _gameContext.GetEntityWithEntityId(
                    task.WarehouseTaskProductEntityId);
                bool awaitManualHandoff = task.isInboundToStorageTask
                    ? ShouldAwaitInboundHandoff(product)
                    : ShouldAwaitCustomerHandoff(task, product);
                if (awaitManualHandoff)
                    continue;

                task.isDestructed = true;
                ResetWorker(task);
            }
        }

        private static bool ShouldAwaitInboundHandoff(GameEntity product) =>
            product != null && !product.isDestructed &&
            product.isInboundProduct && product.hasDeliverySlotIndex &&
            product.isInteractable;

        private bool ShouldAwaitCustomerHandoff(GameEntity task,
            GameEntity product)
        {
            if (!task.hasWarehouseTaskCustomerVisitEntityId ||
                !task.hasWarehouseTaskOrderLineEntityId)
            {
                throw new InvalidOperationException(
                    $"Blocked customer task {task.EntityId} has incomplete target relations.");
            }
            if (!IsRestoredShelfProduct(product))
                return false;

            GameEntity visit = _gameContext.GetEntityWithEntityId(
                task.WarehouseTaskCustomerVisitEntityId);
            GameEntity line = _gameContext.GetEntityWithEntityId(
                task.WarehouseTaskOrderLineEntityId);
            if (visit == null || visit.isDestructed ||
                line == null || line.isDestructed)
            {
                return false;
            }
            if (!visit.isCustomerVisit || !visit.isOrder ||
                !visit.hasEntityId ||
                visit.EntityId != task.WarehouseTaskCustomerVisitEntityId ||
                !line.isOrderLine || !line.hasEntityId ||
                line.EntityId != task.WarehouseTaskOrderLineEntityId ||
                !line.hasOrderEntityId || line.OrderEntityId != visit.EntityId ||
                !line.hasProductType || !line.hasRequiredProductCount ||
                !line.hasLoadedProductCount ||
                product.ProductType != line.ProductType)
            {
                throw new InvalidOperationException(
                    $"Blocked customer task {task.EntityId} has invalid manual handoff.");
            }
            return visit.isCustomerVisitLoading &&
                   line.LoadedProductCount < line.RequiredProductCount;
        }

        private static bool IsRestoredShelfProduct(GameEntity product) =>
            product != null && !product.isDestructed && product.isProduct &&
            product.hasEntityId && product.hasProductType && product.isInStock &&
            !product.isInboundProduct && !product.isLoaded &&
            product.isInteractable && product.hasStorageZoneEntityId &&
            product.hasStorageSlotIndex && !product.hasReservedStorageSlotIndex &&
            !product.hasReservedOrderLineEntityId &&
            !product.hasCarrierEntityId && !product.isLooseProduct &&
            !product.hasOrderLineEntityId && !product.hasLoadingSlotIndex &&
            !product.hasTrolleyEntityId && !product.hasTrolleySlotIndex;

        private void ResetWorker(GameEntity task)
        {
            GameEntity worker =
                _gameContext.GetEntityWithWarehouseWorkerStoreEntityId(
                    task.WarehouseTaskStoreEntityId);
            if (worker == null || worker.isDestructed)
                return;
            if (!worker.isWarehouseWorker || !worker.hasWarehouseWorkerStatus)
            {
                throw new InvalidOperationException(
                    $"Blocked task {task.EntityId} references invalid worker.");
            }
            if (worker.WarehouseWorkerStatus != WarehouseWorkerStatusId.Blocked)
                return;

            worker.ReplaceWarehouseWorkerStatus(worker.isWorkerShiftActive
                ? WarehouseWorkerStatusId.Idle
                : WarehouseWorkerStatusId.OffShift);
        }

        private static void ValidateRole(GameEntity task)
        {
            if (task.isInboundToStorageTask == task.isStockToCustomerLoadingTask)
            {
                throw new InvalidOperationException(
                    $"Warehouse task {task.EntityId} must have exactly one task role.");
            }
        }
    }
}
