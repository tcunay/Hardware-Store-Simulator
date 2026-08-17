using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Navigation;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class RecoverBlockedWorkerTrolleyRunSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IWorkerNavigationService _navigation;
        private readonly IGroup<GameEntity> _runs;
        private readonly List<GameEntity> _products = new(3);

        public RecoverBlockedWorkerTrolleyRunSystem(GameContext gameContext,
            IWorkerNavigationService navigation)
        {
            _gameContext = gameContext;
            _navigation = navigation;
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
            foreach (GameEntity run in _runs)
            {
                if (run.WarehouseTaskStep != WarehouseTaskStepId.Blocked)
                    continue;
                if (run.WarehouseTaskBlockReason == WarehouseTaskBlockReasonId.None)
                    throw new InvalidOperationException(
                        $"Blocked worker-trolley run {run.EntityId} has no reason.");
                CollectAndValidateProducts(run, out bool restoreProducts);
                bool requiresRecovery = run.hasAssignedWorkerEntityId ||
                                        restoreProducts;
                if (!requiresRecovery)
                    continue;

                GameEntity worker = run.hasAssignedWorkerEntityId
                    ? _gameContext.GetEntityWithEntityId(
                        run.AssignedWorkerEntityId)
                    : null;
                GameEntity trolley = _gameContext.GetEntityWithEntityId(
                    run.WarehouseTaskWorkerTrolleyEntityId);
                ValidateRecoveryTargets(run, worker, trolley);

                if (restoreProducts)
                {
                    foreach (GameEntity product in _products)
                        RestoreProduct(product);
                }
                if (trolley != null && !trolley.isDestructed)
                    NormalizeTrolley(trolley, worker);
                else if (trolley != null &&
                         trolley.hasTrolleyPusherEntityId)
                {
                    if (!run.hasAssignedWorkerEntityId ||
                        trolley.TrolleyPusherEntityId !=
                        run.AssignedWorkerEntityId)
                        throw new InvalidOperationException(
                            "Destructed worker trolley has an unrelated pusher.");
                    trolley.RemoveTrolleyPusherEntityId();
                }
                if (worker != null && !worker.isDestructed)
                    ResetWorker(worker);
                if (run.hasAssignedWorkerEntityId)
                    run.RemoveAssignedWorkerEntityId();
            }
        }

        private void CollectAndValidateProducts(GameEntity run,
            out bool restoreProducts)
        {
            _products.Clear();
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithWarehouseRunEntityId(run.EntityId))
                _products.Add(product);
            if (_products.Count == 0)
            {
                if (run.hasAssignedWorkerEntityId)
                    throw new InvalidOperationException(
                        $"Blocked worker-trolley run {run.EntityId} lost its products.");
                restoreProducts = false;
                return;
            }

            bool foundReserved = false;
            bool foundRestored = false;
            foreach (GameEntity product in _products)
            {
                if (product.isDestructed || !product.isProduct ||
                    !product.isInStock || product.isInboundProduct ||
                    product.isLoaded || !product.hasEntityId ||
                    !product.hasProductType ||
                    !product.hasStorageZoneEntityId ||
                    product.hasCarrierEntityId || product.hasOrderLineEntityId ||
                    product.hasLoadingSlotIndex || product.hasTrolleyEntityId ||
                    product.hasTrolleySlotIndex)
                    throw new InvalidOperationException(
                        $"Blocked run {run.EntityId} has an invalid linked product.");

                bool reserved = !product.isInteractable &&
                    product.hasReservedStorageSlotIndex &&
                    product.hasReservedOrderLineEntityId &&
                    product.hasReservedCustomerLoadingSlotIndex &&
                    !product.hasStorageSlotIndex;
                bool restored = product.isInteractable &&
                    product.hasStorageSlotIndex &&
                    !product.hasReservedStorageSlotIndex &&
                    !product.hasReservedOrderLineEntityId &&
                    !product.hasReservedCustomerLoadingSlotIndex &&
                    !product.hasWorkerTrolleyEntityId &&
                    !product.hasWorkerTrolleySlotIndex;
                if (reserved == restored)
                    throw new InvalidOperationException(
                        $"Blocked run product {product.EntityId} has an invalid phase.");
                foundReserved |= reserved;
                foundRestored |= restored;
                if (foundReserved && foundRestored)
                    throw new InvalidOperationException(
                        $"Blocked run {run.EntityId} was only partially recovered.");

                bool hasWorkerTrolleyPlacement =
                    product.hasWorkerTrolleyEntityId ||
                    product.hasWorkerTrolleySlotIndex;
                if (reserved && hasWorkerTrolleyPlacement &&
                    (!product.hasWorkerTrolleyEntityId ||
                     !product.hasWorkerTrolleySlotIndex ||
                     product.WorkerTrolleyEntityId !=
                     run.WarehouseTaskWorkerTrolleyEntityId))
                    throw new InvalidOperationException(
                        $"Blocked run product {product.EntityId} has invalid trolley placement.");
                GameEntity storage = _gameContext.GetEntityWithEntityId(
                    product.StorageZoneEntityId);
                int slotIndex = reserved
                    ? product.ReservedStorageSlotIndex
                    : product.StorageSlotIndex;
                if (storage == null || storage.isDestructed ||
                    !storage.isStorageZone || !storage.hasSlots ||
                    slotIndex < 0 || slotIndex >= storage.Slots.Length)
                    throw new InvalidOperationException(
                        $"Blocked run product {product.EntityId} has invalid storage slot.");
            }

            if (foundReserved &&
                (_products.Count != run.WarehouseRunProductCount ||
                 _products.Count < 2))
                throw new InvalidOperationException(
                    $"Blocked worker-trolley run {run.EntityId} lost its products.");
            restoreProducts = foundReserved;
        }

        private void ValidateRecoveryTargets(GameEntity run,
            GameEntity worker, GameEntity trolley)
        {
            if (worker != null && !worker.isDestructed &&
                (!worker.isWarehouseWorker || !worker.hasEntityId ||
                 !worker.hasNavigationAgent || !worker.hasWarehouseWorkerStatus))
                throw new InvalidOperationException(
                    $"Blocked run {run.EntityId} references invalid worker.");
            if (trolley != null && !trolley.isDestructed &&
                (!trolley.isWorkerTrolley || !trolley.hasEntityId ||
                 !trolley.hasOccupiedTrolleySlotCount ||
                 !trolley.hasWorkerTrolleyHomePosition ||
                 !trolley.hasWorkerTrolleyHomeRotation ||
                 !trolley.hasTransform || !trolley.hasRigidbody))
                throw new InvalidOperationException(
                    $"Blocked run {run.EntityId} references invalid trolley.");
            if (trolley != null && !trolley.isDestructed &&
                trolley.hasTrolleyPusherEntityId)
            {
                GameEntity pusher = _gameContext.GetEntityWithEntityId(
                    trolley.TrolleyPusherEntityId);
                if (pusher != null && !pusher.isDestructed &&
                    (!run.hasAssignedWorkerEntityId ||
                     pusher.EntityId != run.AssignedWorkerEntityId))
                    throw new InvalidOperationException(
                        $"Worker trolley {trolley.EntityId} has an unrelated pusher.");
            }
            if (trolley != null && trolley.isDestructed &&
                trolley.hasTrolleyPusherEntityId &&
                (!run.hasAssignedWorkerEntityId ||
                 trolley.TrolleyPusherEntityId !=
                 run.AssignedWorkerEntityId))
                throw new InvalidOperationException(
                    "Destructed worker trolley has an unrelated pusher.");
        }

        private static void RestoreProduct(GameEntity product)
        {
            int storageSlotIndex = product.ReservedStorageSlotIndex;
            if (product.hasWorkerTrolleyEntityId)
                product.RemoveWorkerTrolleyEntityId();
            if (product.hasWorkerTrolleySlotIndex)
                product.RemoveWorkerTrolleySlotIndex();
            product.RemoveReservedStorageSlotIndex();
            product.RemoveReservedOrderLineEntityId();
            product.RemoveReservedCustomerLoadingSlotIndex();
            product.AddStorageSlotIndex(storageSlotIndex);
            product.isInteractable = true;
            product.isProductPlacementDirty = true;
        }

        private static void NormalizeTrolley(GameEntity trolley,
            GameEntity worker)
        {
            trolley.ReplaceOccupiedTrolleySlotCount(0);
            trolley.Rigidbody.position = trolley.WorkerTrolleyHomePosition;
            trolley.Rigidbody.rotation = trolley.WorkerTrolleyHomeRotation;
            trolley.Transform.SetPositionAndRotation(
                trolley.WorkerTrolleyHomePosition,
                trolley.WorkerTrolleyHomeRotation);
            if (trolley.hasTrolleyPusherEntityId)
                trolley.RemoveTrolleyPusherEntityId();
        }

        private void ResetWorker(GameEntity worker)
        {
            _navigation.Stop(worker.NavigationAgent);
            worker.isHandsOccupied = false;
            worker.isCarryingProduct = false;
            worker.isPushingWorkerTrolley = false;
            _navigation.SetAutomaticRotation(
                worker.NavigationAgent, enabled: true);
            worker.ReplaceWarehouseWorkerStatus(WarehouseWorkerStatusId.Blocked);
        }
    }
}
