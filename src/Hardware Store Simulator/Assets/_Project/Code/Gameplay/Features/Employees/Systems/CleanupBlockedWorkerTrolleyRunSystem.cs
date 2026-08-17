using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class CleanupBlockedWorkerTrolleyRunSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _runs;
        private readonly List<GameEntity> _runBuffer = new(2);
        private readonly List<GameEntity> _products = new(3);

        public CleanupBlockedWorkerTrolleyRunSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _runs = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.WorkerTrolleyCustomerLoadingRun,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseTaskStoreEntityId,
                    GameMatcher.WarehouseTaskCustomerVisitEntityId,
                    GameMatcher.WarehouseTaskWorkerTrolleyEntityId,
                    GameMatcher.WarehouseRunProductCount,
                    GameMatcher.WarehouseTaskStep,
                    GameMatcher.WarehouseTaskBlockReason)
                .NoneOf(
                    GameMatcher.InboundToStorageTask,
                    GameMatcher.StockToCustomerLoadingTask,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity run in _runs.GetEntities(_runBuffer))
            {
                if (run.WarehouseTaskStep != WarehouseTaskStepId.Blocked)
                    continue;
                if (run.WarehouseTaskBlockReason == WarehouseTaskBlockReasonId.None)
                    throw new InvalidOperationException(
                        $"Blocked worker-trolley run {run.EntityId} has no reason.");
                if (run.hasAssignedWorkerEntityId)
                    continue;

                CollectLinkedProducts(run);
                bool recoveryPending = false;
                foreach (GameEntity product in _products)
                {
                    recoveryPending |= product.hasReservedStorageSlotIndex ||
                                       product.hasReservedOrderLineEntityId ||
                                       product.hasReservedCustomerLoadingSlotIndex ||
                                       product.hasWorkerTrolleyEntityId ||
                                       product.hasWorkerTrolleySlotIndex;
                }
                if (recoveryPending)
                    continue;
                if (ShouldAwaitManualHandoff(run))
                    continue;

                foreach (GameEntity product in _products)
                    product.RemoveWarehouseRunEntityId();
                run.RemoveWarehouseTaskWorkerTrolleyEntityId();
                run.isDestructed = true;
                ResetWorker(run.WarehouseTaskStoreEntityId);
            }
        }

        private void CollectLinkedProducts(GameEntity run)
        {
            _products.Clear();
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithWarehouseRunEntityId(run.EntityId))
            {
                if (product.isDestructed || !product.isProduct ||
                    !product.hasEntityId || !product.hasProductType ||
                    !product.isInStock || product.isInboundProduct ||
                    product.isLoaded || !product.hasStorageZoneEntityId)
                    throw new InvalidOperationException(
                        $"Blocked run {run.EntityId} has an invalid linked product.");
                _products.Add(product);
            }
        }

        private bool ShouldAwaitManualHandoff(GameEntity run)
        {
            GameEntity visit = _gameContext.GetEntityWithEntityId(
                run.WarehouseTaskCustomerVisitEntityId);
            if (visit == null || visit.isDestructed ||
                !visit.isCustomerVisit || !visit.isOrder ||
                !visit.isCustomerVisitLoading || !visit.hasEntityId)
                return false;

            foreach (GameEntity product in _products)
            {
                if (!product.isInteractable || !product.hasStorageSlotIndex ||
                    product.hasCarrierEntityId ||
                    product.hasReservedStorageSlotIndex ||
                    product.hasReservedOrderLineEntityId ||
                    product.hasReservedCustomerLoadingSlotIndex ||
                    product.hasWorkerTrolleyEntityId ||
                    product.hasWorkerTrolleySlotIndex)
                    throw new InvalidOperationException(
                        $"Blocked run product {product.EntityId} is not restored.");
                foreach (GameEntity line in
                         _gameContext.GetEntitiesWithOrderEntityId(visit.EntityId))
                {
                    if (line.isDestructed || !line.isOrderLine ||
                        !line.hasProductType || !line.hasRequiredProductCount ||
                        !line.hasLoadedProductCount)
                        throw new InvalidOperationException(
                            $"Visit {visit.EntityId} has an invalid order line.");
                    if (line.ProductType == product.ProductType &&
                        line.LoadedProductCount < line.RequiredProductCount)
                        return true;
                }
            }
            return false;
        }

        private void ResetWorker(int storeEntityId)
        {
            GameEntity worker =
                _gameContext.GetEntityWithWarehouseWorkerStoreEntityId(storeEntityId);
            if (worker == null || worker.isDestructed)
                return;
            if (!worker.isWarehouseWorker || !worker.hasWarehouseWorkerStatus)
                throw new InvalidOperationException(
                    $"Store {storeEntityId} has an invalid warehouse worker.");
            if (worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.Blocked)
                worker.ReplaceWarehouseWorkerStatus(worker.isWorkerShiftActive
                    ? WarehouseWorkerStatusId.Idle
                    : WarehouseWorkerStatusId.OffShift);
        }
    }
}
