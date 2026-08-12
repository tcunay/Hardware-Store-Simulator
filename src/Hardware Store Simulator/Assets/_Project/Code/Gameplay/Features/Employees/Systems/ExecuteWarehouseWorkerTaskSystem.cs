using System;
using Entitas;
using HardwareStore.Gameplay.Common.Navigation;
using HardwareStore.Gameplay.Common.Time;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class ExecuteWarehouseWorkerTaskSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly WarehouseWorkerConfig _config;
        private readonly IWorkerNavigationService _navigation;
        private readonly ITimeService _time;
        private readonly IGroup<GameEntity> _workers;
        private readonly IGroup<GameEntity> _tasks;
        private readonly IGroup<GameEntity> _stockedProducts;
        private readonly IGroup<GameEntity> _reservedStockProducts;

        public ExecuteWarehouseWorkerTaskSystem(GameContext gameContext,
            IStaticDataService staticData, IWorkerNavigationService navigation,
            ITimeService time)
        {
            _gameContext = gameContext;
            _config = staticData.WarehouseWorker;
            _navigation = navigation;
            _time = time;
            _workers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseWorker,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseWorkerStoreEntityId,
                    GameMatcher.WarehouseWorkerStatus,
                    GameMatcher.View,
                    GameMatcher.Transform,
                    GameMatcher.NavigationAgent,
                    GameMatcher.CarryAnchor,
                    GameMatcher.WarehouseWorkerPickupPosition,
                    GameMatcher.WarehouseWorkerPickupRotation,
                    GameMatcher.WarehouseWorkerStoragePosition,
                    GameMatcher.WarehouseWorkerStorageRotation)
                .NoneOf(GameMatcher.Destructed));
            _tasks = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.InboundToStorageTask,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseTaskStoreEntityId,
                    GameMatcher.WarehouseTaskStorageZoneEntityId,
                    GameMatcher.WarehouseTaskProductEntityId,
                    GameMatcher.WarehouseTaskStep,
                    GameMatcher.WarehouseTaskTimeoutRemaining)
                .NoneOf(GameMatcher.Destructed));
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
            foreach (GameEntity worker in _workers)
            {
                GameEntity task = _gameContext.GetEntityWithAssignedWorkerEntityId(
                    worker.EntityId);
                if (task == null)
                    task = TryAssignTask(worker);
                if (task == null)
                    continue;

                TickTimeout(task);
                if (task.WarehouseTaskStep == WarehouseTaskStepId.Blocked)
                    continue;

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
                            $"Assigned task {task.EntityId} has invalid step " +
                            $"{task.WarehouseTaskStep}.");
                }
            }
        }

        private GameEntity TryAssignTask(GameEntity worker)
        {
            if (!worker.isWorkerShiftActive ||
                worker.WarehouseWorkerStatus != WarehouseWorkerStatusId.Idle)
                return null;

            GameEntity store = _gameContext.GetEntityWithEntityId(
                worker.WarehouseWorkerStoreEntityId);
            if (store == null || !store.isStore || store.isDestructed ||
                !store.hasStorageZoneEntityId)
                throw new InvalidOperationException(
                    $"Warehouse worker {worker.EntityId} has invalid store relation.");
            if (!store.isStoreOpen)
                return null;

            GameEntity selected = null;
            foreach (GameEntity task in _tasks)
            {
                if (task.WarehouseTaskStoreEntityId != store.EntityId ||
                    task.WarehouseTaskStep != WarehouseTaskStepId.Available ||
                    task.hasAssignedWorkerEntityId)
                    continue;
                if (selected == null || task.EntityId < selected.EntityId)
                    selected = task;
            }

            if (selected == null)
                return null;

            selected.AddAssignedWorkerEntityId(worker.EntityId);
            selected.ReplaceWarehouseTaskStep(WarehouseTaskStepId.MovingToPickup);
            selected.ReplaceWarehouseTaskBlockReason(WarehouseTaskBlockReasonId.None);
            selected.ReplaceWarehouseTaskTimeoutRemaining(_config.TaskTimeout);
            worker.ReplaceWarehouseWorkerStatus(
                WarehouseWorkerStatusId.MovingToPickup);
            return selected;
        }

        private void TickTimeout(GameEntity task)
        {
            float remaining = Mathf.Max(0f,
                task.WarehouseTaskTimeoutRemaining - _time.DeltaTime);
            task.ReplaceWarehouseTaskTimeoutRemaining(remaining);
            if (remaining <= 0f)
                Block(task, WarehouseTaskBlockReasonId.TimedOut);
        }

        private void ExecutePickupStep(GameEntity worker, GameEntity task)
        {
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
                Block(task, blockReason);
        }

        private void CompleteStorage(GameEntity worker, GameEntity task,
            GameEntity product)
        {
            if (!task.hasWarehouseTaskReservedStorageSlotIndex)
                throw new InvalidOperationException(
                    $"Warehouse task {task.EntityId} has no storage reservation.");
            GameEntity storageZone = _gameContext.GetEntityWithEntityId(
                task.WarehouseTaskStorageZoneEntityId);
            if (storageZone == null || !storageZone.isStorageZone ||
                !storageZone.hasSlots || storageZone.isDestructed)
                throw new InvalidOperationException(
                    $"Warehouse task {task.EntityId} references invalid storage zone.");

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
                throw new InvalidOperationException(
                    $"Task {ownerTask.EntityId} reserves invalid storage slot {slotIndex}.");
            foreach (GameEntity product in _stockedProducts)
            {
                if (product.StorageZoneEntityId == storageZone.EntityId &&
                    product.StorageSlotIndex == slotIndex)
                    throw new InvalidOperationException(
                        $"Task {ownerTask.EntityId} storage slot is already occupied.");
            }
            foreach (GameEntity product in _reservedStockProducts)
            {
                if (product.StorageZoneEntityId == storageZone.EntityId &&
                    product.ReservedStorageSlotIndex == slotIndex)
                    throw new InvalidOperationException(
                        $"Task {ownerTask.EntityId} storage slot is already reserved.");
            }
            foreach (GameEntity task in _tasks)
            {
                if (task != ownerTask && task.hasWarehouseTaskReservedStorageSlotIndex &&
                    task.WarehouseTaskStorageZoneEntityId == storageZone.EntityId &&
                    task.WarehouseTaskReservedStorageSlotIndex == slotIndex)
                    throw new InvalidOperationException(
                        $"Task {ownerTask.EntityId} storage slot is reserved twice.");
            }
        }

        private GameEntity GetTaskProduct(GameEntity task)
        {
            GameEntity product = _gameContext.GetEntityWithEntityId(
                task.WarehouseTaskProductEntityId);
            if (product == null || !product.isProduct || product.isDestructed ||
                !product.hasProductType)
                throw new InvalidOperationException(
                    $"Warehouse task {task.EntityId} references missing product.");
            return product;
        }

        private static void Block(GameEntity task,
            WarehouseTaskBlockReasonId reason)
        {
            task.ReplaceWarehouseTaskStep(WarehouseTaskStepId.Blocked);
            task.ReplaceWarehouseTaskBlockReason(reason);
        }
    }
}
