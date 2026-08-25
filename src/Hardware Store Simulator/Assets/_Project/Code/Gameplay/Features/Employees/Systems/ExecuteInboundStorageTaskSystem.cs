using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common;
using HardwareStore.Gameplay.Common.Navigation;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class ExecuteInboundStorageTaskSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly WarehouseWorkerConfig _config;
        private readonly IWorkerNavigationService _navigation;
        private readonly IGroup<GameEntity> _tasks;
        private readonly List<GameEntity> _buffer = new(4);
        private readonly IGroup<GameEntity> _stockedProducts;
        private readonly IGroup<GameEntity> _reservedStockProducts;

        public ExecuteInboundStorageTaskSystem(GameContext gameContext,
            IStaticDataService staticData, IWorkerNavigationService navigation)
        {
            _gameContext = gameContext;
            _config = staticData.WarehouseWorker;
            _navigation = navigation;
            _tasks = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.InboundToStorageTask,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseTaskStoreEntityId,
                    GameMatcher.WarehouseTaskStorageZoneEntityId,
                    GameMatcher.WarehouseTaskProductEntityId,
                    GameMatcher.WarehouseTaskReservedStorageSlotIndex,
                    GameMatcher.WarehouseTaskStep,
                    GameMatcher.WarehouseTaskBlockReason,
                    GameMatcher.WarehouseTaskTimeoutRemaining)
                .NoneOf(
                    GameMatcher.StockToCustomerLoadingTask,
                    GameMatcher.Destructed));
            _stockedProducts = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Product,
                GameMatcher.EntityId,
                GameMatcher.InStock,
                GameMatcher.StorageZoneEntityId,
                GameMatcher.StorageSlotIndex));
            _reservedStockProducts = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Product,
                GameMatcher.EntityId,
                GameMatcher.InStock,
                GameMatcher.StorageZoneEntityId,
                GameMatcher.ReservedStorageSlotIndex));
        }

        public void Execute()
        {
            foreach (GameEntity task in _tasks.GetEntities(_buffer))
            {
                if (IsWorkerTrolleyBatchTask(task))
                    continue;
                if (!task.hasAssignedWorkerEntityId)
                {
                    if (task.WarehouseTaskStep is WarehouseTaskStepId.Available or
                        WarehouseTaskStepId.Blocked)
                    {
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"Inbound warehouse task {task.EntityId} is moving without a worker.");
                }
                if (task.WarehouseTaskStep == WarehouseTaskStepId.Blocked)
                    continue;

                GameEntity worker = _gameContext.GetEntityWithEntityId(
                    task.AssignedWorkerEntityId);
                if (worker == null || worker.isDestructed)
                    continue;
                ValidateWorker(task, worker);

                switch (task.WarehouseTaskStep)
                {
                    case WarehouseTaskStepId.MovingToPickup:
                        ExecutePickupStep(worker, task);
                        break;
                    case WarehouseTaskStepId.MovingToStorage:
                        ExecuteStorageStep(worker, task);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Assigned inbound task {task.EntityId} has invalid step " +
                            $"{task.WarehouseTaskStep}.");
                }
            }
        }

        private bool IsWorkerTrolleyBatchTask(GameEntity task)
        {
            if (task.isWorkerTrolleyInboundStorageRun)
                return true;
            GameEntity product = _gameContext.GetEntityWithEntityId(
                task.WarehouseTaskProductEntityId);
            return product != null && !product.isDestructed &&
                   product.hasWarehouseRunEntityId;
        }

        private void ExecutePickupStep(GameEntity worker, GameEntity task)
        {
            if (worker.WarehouseWorkerStatus != WarehouseWorkerStatusId.MovingToPickup)
                throw InvalidWorkerStatus(worker, task);
            GameEntity product = GetTaskProduct(task);
            if (!product.isInboundProduct || !product.hasDeliveryEntityId ||
                !product.hasDeliverySlotIndex || product.hasCarrierEntityId ||
                product.isInteractable)
            {
                throw new InvalidOperationException(
                    $"Warehouse task {task.EntityId} pickup product has invalid state.");
            }

            if (!HasReached(worker, worker.WarehouseWorkerPickupPosition))
            {
                Navigate(worker, task, worker.WarehouseWorkerPickupPosition,
                    WarehouseTaskBlockReasonId.NoPickupPath);
                return;
            }

            _navigation.Stop(worker.NavigationAgent);
            worker.Transform.rotation = worker.WarehouseWorkerPickupRotation;
            int deliverySlotIndex = product.DeliverySlotIndex;
            product.RemoveDeliverySlotIndex();
            product.AddReservedDeliverySlotIndex(deliverySlotIndex);
            product.AddCarrierEntityId(worker.EntityId);
            product.isProductPlacementDirty = true;
            worker.isHandsOccupied = true;
            worker.isCarryingProduct = true;
            task.ReplaceWarehouseTaskStep(WarehouseTaskStepId.MovingToStorage);
            task.ReplaceWarehouseTaskTimeoutRemaining(_config.TaskTimeout);
            worker.ReplaceWarehouseWorkerStatus(
                WarehouseWorkerStatusId.MovingToStorage);
        }

        private void ExecuteStorageStep(GameEntity worker, GameEntity task)
        {
            if (worker.WarehouseWorkerStatus != WarehouseWorkerStatusId.MovingToStorage)
                throw InvalidWorkerStatus(worker, task);
            GameEntity product = GetTaskProduct(task);
            if (!product.isInboundProduct || !product.hasDeliveryEntityId ||
                !product.hasReservedDeliverySlotIndex ||
                !product.hasCarrierEntityId ||
                product.CarrierEntityId != worker.EntityId ||
                !worker.isHandsOccupied || !worker.isCarryingProduct ||
                product.isInteractable)
            {
                throw new InvalidOperationException(
                    $"Warehouse task {task.EntityId} carried product has invalid state.");
            }

            if (!HasReached(worker, worker.WarehouseWorkerStoragePosition))
            {
                Navigate(worker, task, worker.WarehouseWorkerStoragePosition,
                    WarehouseTaskBlockReasonId.NoStoragePath);
                return;
            }

            CompleteStorage(worker, task, product);
        }

        private bool HasReached(GameEntity worker, Vector3 destination)
        {
            float tolerance = Mathf.Max(_config.StoppingDistance, 0.05f);
            return _navigation.HasReachedDestination(worker.NavigationAgent,
                worker.Transform.position, destination, tolerance);
        }

        private void Navigate(GameEntity worker, GameEntity task,
            Vector3 destination, WarehouseTaskBlockReasonId blockReason)
        {
            WorkerNavigationStateId state = _navigation.GetState(
                worker.NavigationAgent);
            if (state == WorkerNavigationStateId.Moving)
                return;
            if (!_navigation.TrySetDestination(worker.NavigationAgent,
                    destination, _config.NavigationSampleRadius))
            {
                Block(task, blockReason);
            }
        }

        private void CompleteStorage(GameEntity worker, GameEntity task,
            GameEntity product)
        {
            GameEntity storageZone = _gameContext.GetEntityWithEntityId(
                task.WarehouseTaskStorageZoneEntityId);
            if (storageZone == null || !storageZone.isStorageZone ||
                !storageZone.hasSlots || storageZone.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Warehouse task {task.EntityId} references invalid storage zone.");
            }

            int slotIndex = task.WarehouseTaskReservedStorageSlotIndex;
            ValidateExclusiveReservation(task, storageZone, slotIndex);
            _navigation.Stop(worker.NavigationAgent);
            worker.Transform.rotation = worker.WarehouseWorkerStorageRotation;
            worker.isHandsOccupied = false;
            worker.isCarryingProduct = false;
            product.RemoveCarrierEntityId();
            product.RemoveReservedDeliverySlotIndex();
            product.isInboundProduct = false;
            product.isInStock = true;
            product.isInteractable = true;
            product.AddStorageZoneEntityId(storageZone.EntityId);
            product.AddStorageSlotIndex(slotIndex);
            product.isProductPlacementDirty = true;
            product.isProductStocked = true;

            task.RemoveAssignedWorkerEntityId();
            task.RemoveWarehouseTaskReservedStorageSlotIndex();
            task.isDestructed = true;
            worker.ReplaceWarehouseWorkerStatus(worker.isWorkerShiftActive
                ? WarehouseWorkerStatusId.Idle
                : WarehouseWorkerStatusId.OffShift);
        }

        private void ValidateExclusiveReservation(GameEntity ownerTask,
            GameEntity storageZone, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= storageZone.Slots.Length)
            {
                throw new InvalidOperationException(
                    $"Task {ownerTask.EntityId} reserves invalid storage slot {slotIndex}.");
            }
            foreach (GameEntity product in _stockedProducts)
            {
                if (product.StorageZoneEntityId == storageZone.EntityId &&
                    product.StorageSlotIndex == slotIndex)
                {
                    throw new InvalidOperationException(
                        $"Task {ownerTask.EntityId} storage slot is already occupied.");
                }
            }
            foreach (GameEntity product in _reservedStockProducts)
            {
                if (product.StorageZoneEntityId == storageZone.EntityId &&
                    product.ReservedStorageSlotIndex == slotIndex)
                {
                    throw new InvalidOperationException(
                        $"Task {ownerTask.EntityId} storage slot is already reserved.");
                }
            }
            foreach (GameEntity task in _tasks)
            {
                if (task != ownerTask &&
                    task.WarehouseTaskStorageZoneEntityId == storageZone.EntityId &&
                    task.WarehouseTaskReservedStorageSlotIndex == slotIndex)
                {
                    throw new InvalidOperationException(
                        $"Task {ownerTask.EntityId} storage slot is reserved twice.");
                }
            }
        }

        private GameEntity GetTaskProduct(GameEntity task)
        {
            GameEntity product = _gameContext.GetEntityWithEntityId(
                task.WarehouseTaskProductEntityId);
            if (product == null || !product.isProduct || product.isDestructed ||
                !product.hasProductType || !product.hasDeliveryEntityId ||
                !product.hasPurchaseOrderLineEntityId)
            {
                throw new InvalidOperationException(
                    $"Warehouse task {task.EntityId} references missing product.");
            }

            InboundProductManifestValidator.Validate(_gameContext, product);
            GameEntity delivery = _gameContext.GetEntityWithEntityId(
                product.DeliveryEntityId);
            GameEntity line = _gameContext.GetEntityWithEntityId(
                product.PurchaseOrderLineEntityId);
            if (delivery == null || !delivery.isDelivery ||
                !delivery.isDeliveryActive || delivery.isDestructed ||
                !delivery.hasDeliveryPurchaseOrderEntityId ||
                line == null || !line.isPurchaseOrderLine || line.isDestructed ||
                !line.hasPurchaseOrderEntityId ||
                line.PurchaseOrderEntityId != delivery.DeliveryPurchaseOrderEntityId ||
                !line.hasProductType || line.ProductType != product.ProductType)
            {
                throw new InvalidOperationException(
                    $"Warehouse task {task.EntityId} product has an invalid purchase " +
                    "manifest relation.");
            }
            return product;
        }

        private static void ValidateWorker(GameEntity task, GameEntity worker)
        {
            if (!worker.isWarehouseWorker || !worker.hasEntityId ||
                worker.EntityId != task.AssignedWorkerEntityId ||
                !worker.hasWarehouseWorkerStoreEntityId ||
                worker.WarehouseWorkerStoreEntityId !=
                task.WarehouseTaskStoreEntityId ||
                !worker.hasWarehouseWorkerStatus || !worker.hasTransform ||
                !worker.hasNavigationAgent ||
                !worker.hasWarehouseWorkerPickupPosition ||
                !worker.hasWarehouseWorkerPickupRotation ||
                !worker.hasWarehouseWorkerStoragePosition ||
                !worker.hasWarehouseWorkerStorageRotation)
            {
                throw new InvalidOperationException(
                    $"Inbound task {task.EntityId} references invalid worker.");
            }
        }

        private static InvalidOperationException InvalidWorkerStatus(
            GameEntity worker, GameEntity task) =>
            new($"Worker {worker.EntityId} status {worker.WarehouseWorkerStatus} does not " +
                $"match inbound task {task.EntityId} step {task.WarehouseTaskStep}.");

        private static void Block(GameEntity task,
            WarehouseTaskBlockReasonId reason)
        {
            task.ReplaceWarehouseTaskStep(WarehouseTaskStepId.Blocked);
            task.ReplaceWarehouseTaskBlockReason(reason);
        }
    }
}
