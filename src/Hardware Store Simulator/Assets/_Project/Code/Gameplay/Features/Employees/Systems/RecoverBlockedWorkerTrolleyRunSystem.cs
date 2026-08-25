using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common;
using HardwareStore.Gameplay.Common.Navigation;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Features.Employees;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class RecoverBlockedWorkerTrolleyRunSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IWorkerNavigationService _navigation;
        private readonly IGroup<GameEntity> _runs;
        private readonly List<GameEntity> _runBuffer = new(2);
        private readonly List<GameEntity> _products = new(3);
        private readonly List<GameEntity> _inboundTasks = new(3);

        public RecoverBlockedWorkerTrolleyRunSystem(GameContext gameContext,
            IWorkerNavigationService navigation)
        {
            _gameContext = gameContext;
            _navigation = navigation;
            _runs = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseTaskStoreEntityId,
                    GameMatcher.WarehouseTaskWorkerTrolleyEntityId,
                    GameMatcher.WarehouseRunProductCount,
                    GameMatcher.WarehouseTaskStep,
                    GameMatcher.WarehouseTaskBlockReason)
                .NoneOf(
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
                bool inbound = run.isWorkerTrolleyInboundStorageRun;
                ValidateRunRole(run, inbound);
                bool restoreProducts;
                if (inbound)
                {
                    CollectAndValidateInboundProducts(run);
                    restoreProducts = true;
                }
                else
                {
                    CollectAndValidateProducts(run, out restoreProducts);
                }
                bool requiresRecovery = inbound ||
                                        run.hasAssignedWorkerEntityId ||
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
                    {
                        if (inbound)
                            RestoreInboundProduct(product);
                        else
                            RestoreProduct(product);
                    }
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
                if (inbound)
                    DestroyInboundTasks(run);
                else if (run.hasWarehouseTaskWorkerTrolleyEntityId)
                    run.RemoveWarehouseTaskWorkerTrolleyEntityId();
                if (worker != null && !worker.isDestructed)
                {
                    if (inbound)
                        ResetInboundWorker(worker);
                    else
                        ResetWorker(worker);
                }
                if (run.hasAssignedWorkerEntityId)
                    run.RemoveAssignedWorkerEntityId();
            }
        }

        private static void ValidateRunRole(GameEntity run, bool inbound)
        {
            bool validInbound = inbound && run.isInboundToStorageTask &&
                !run.isWorkerTrolleyCustomerLoadingRun &&
                run.hasWarehouseTaskStorageZoneEntityId &&
                run.hasWarehouseTaskProductEntityId &&
                run.hasWarehouseTaskReservedStorageSlotIndex;
            bool validOutbound = !inbound &&
                run.isWorkerTrolleyCustomerLoadingRun &&
                !run.isInboundToStorageTask &&
                run.hasWarehouseTaskCustomerVisitEntityId;
            if (!validInbound && !validOutbound ||
                run.WarehouseRunProductCount < 1)
                throw new InvalidOperationException(
                    $"Blocked worker-trolley run {run.EntityId} has an invalid role.");
        }

        private void CollectAndValidateInboundProducts(GameEntity run)
        {
            _products.Clear();
            _inboundTasks.Clear();
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithWarehouseRunEntityId(run.EntityId))
                _products.Add(product);
            if (_products.Count != run.WarehouseRunProductCount ||
                _products.Count < 1)
                throw new InvalidOperationException(
                    $"Blocked inbound trolley run {run.EntityId} lost its products.");

            bool foundAtDelivery = false;
            bool foundOnTrolley = false;
            foreach (GameEntity product in _products)
            {
                InboundProductManifestValidator.Validate(_gameContext, product);
                GameEntity task =
                    _gameContext.GetEntityWithWarehouseTaskProductEntityId(
                        product.EntityId);
                if (task == null || task.isDestructed ||
                    !task.isWarehouseTask || !task.isInboundToStorageTask ||
                    !task.hasEntityId || !task.hasWarehouseTaskStoreEntityId ||
                    task.WarehouseTaskStoreEntityId !=
                    run.WarehouseTaskStoreEntityId ||
                    !task.hasWarehouseTaskStorageZoneEntityId ||
                    task.WarehouseTaskStorageZoneEntityId !=
                    run.WarehouseTaskStorageZoneEntityId ||
                    !task.hasWarehouseTaskReservedStorageSlotIndex ||
                    (task == run) != task.isWorkerTrolleyInboundStorageRun ||
                    task != run && task.hasAssignedWorkerEntityId)
                    throw new InvalidOperationException(
                        $"Blocked inbound product {product.EntityId} lost its task.");
                _inboundTasks.Add(task);

                bool atDelivery = !product.isInteractable &&
                    product.hasDeliverySlotIndex &&
                    !product.hasReservedDeliverySlotIndex &&
                    !product.hasWorkerTrolleyEntityId &&
                    !product.hasWorkerTrolleySlotIndex;
                bool onTrolley = !product.isInteractable &&
                    !product.hasDeliverySlotIndex &&
                    product.hasReservedDeliverySlotIndex &&
                    product.hasWorkerTrolleyEntityId &&
                    product.WorkerTrolleyEntityId ==
                    run.WarehouseTaskWorkerTrolleyEntityId &&
                    product.hasWorkerTrolleySlotIndex;
                if (atDelivery == onTrolley || product.isDestructed ||
                    !product.isProduct || !product.isInboundProduct ||
                    product.isInStock || product.isLoaded ||
                    product.hasStorageZoneEntityId || product.hasStorageSlotIndex ||
                    product.hasReservedStorageSlotIndex ||
                    product.hasCarrierEntityId || product.hasOrderLineEntityId ||
                    product.hasLoadingSlotIndex || product.isLooseProduct ||
                    product.hasTrolleyEntityId || product.hasTrolleySlotIndex)
                    throw new InvalidOperationException(
                        $"Blocked inbound product {product.EntityId} has an invalid phase.");
                foundAtDelivery |= atDelivery;
                foundOnTrolley |= onTrolley;
                if (foundAtDelivery && foundOnTrolley)
                    throw new InvalidOperationException(
                        $"Blocked inbound run {run.EntityId} was only partially loaded.");
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
                 _products.Count < 1))
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
                (!trolley.isWorkerTrolley || !trolley.isPlatformTrolley ||
                 trolley.isInteractable || !trolley.hasEntityId ||
                 !trolley.hasTrolleyStoreEntityId ||
                 trolley.TrolleyStoreEntityId !=
                 run.WarehouseTaskStoreEntityId ||
                 !trolley.hasWorkerTrolleyStoreEntityId ||
                 trolley.WorkerTrolleyStoreEntityId !=
                 run.WarehouseTaskStoreEntityId ||
                 !trolley.hasOccupiedTrolleySlotCount ||
                 !trolley.hasWorkerTrolleyHomePosition ||
                 !trolley.hasWorkerTrolleyHomeRotation ||
                 !trolley.hasWorkerTrolleyCustomerLoadingPosition ||
                 !trolley.hasWorkerTrolleyCustomerLoadingRotation ||
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

        private static void RestoreInboundProduct(GameEntity product)
        {
            if (product.hasWorkerTrolleyEntityId)
                product.RemoveWorkerTrolleyEntityId();
            if (product.hasWorkerTrolleySlotIndex)
                product.RemoveWorkerTrolleySlotIndex();
            if (product.hasReservedDeliverySlotIndex)
            {
                int deliverySlotIndex = product.ReservedDeliverySlotIndex;
                product.RemoveReservedDeliverySlotIndex();
                product.AddDeliverySlotIndex(deliverySlotIndex);
            }
            if (!product.hasDeliverySlotIndex)
                throw new InvalidOperationException(
                    $"Inbound product {product.EntityId} lost its delivery slot.");
            product.RemoveWarehouseRunEntityId();
            product.isInteractable = true;
            product.isProductPlacementDirty = true;
        }

        private void DestroyInboundTasks(GameEntity run)
        {
            foreach (GameEntity task in _inboundTasks)
            {
                if (task.hasAssignedWorkerEntityId)
                    task.RemoveAssignedWorkerEntityId();
                if (task.hasWarehouseTaskWorkerTrolleyEntityId)
                    task.RemoveWarehouseTaskWorkerTrolleyEntityId();
                if (task.hasWarehouseRunProductCount)
                    task.RemoveWarehouseRunProductCount();
                task.RemoveWarehouseTaskReservedStorageSlotIndex();
                task.isDestructed = true;
            }
            if (!run.isDestructed ||
                run.hasWarehouseTaskWorkerTrolleyEntityId ||
                run.hasWarehouseRunProductCount ||
                run.hasAssignedWorkerEntityId ||
                run.hasWarehouseTaskReservedStorageSlotIndex)
                throw new InvalidOperationException(
                    $"Inbound trolley run {run.EntityId} was not destroyed.");
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
            WorkerTrolleyLeaseUtility.ReleaseLease(trolley);
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

        private void ResetInboundWorker(GameEntity worker)
        {
            _navigation.Stop(worker.NavigationAgent);
            worker.isHandsOccupied = false;
            worker.isCarryingProduct = false;
            worker.isPushingWorkerTrolley = false;
            _navigation.SetAutomaticRotation(
                worker.NavigationAgent, enabled: true);
            worker.ReplaceWarehouseWorkerStatus(worker.isWorkerShiftActive
                ? WarehouseWorkerStatusId.Idle
                : WarehouseWorkerStatusId.OffShift);
        }
    }
}
