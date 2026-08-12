using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class GenerateInboundStorageTaskSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IWarehouseTaskFactory _tasksFactory;
        private readonly IGroup<GameEntity> _workers;
        private readonly IGroup<GameEntity> _products;
        private readonly IGroup<GameEntity> _stockedProducts;
        private readonly IGroup<GameEntity> _reservedStockProducts;
        private readonly IGroup<GameEntity> _tasks;
        private readonly Dictionary<int, int> _occupiedSlots = new();

        public GenerateInboundStorageTaskSystem(GameContext gameContext,
            IWarehouseTaskFactory tasksFactory)
        {
            _gameContext = gameContext;
            _tasksFactory = tasksFactory;
            _workers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseWorker,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseWorkerStoreEntityId,
                    GameMatcher.WarehouseWorkerStatus,
                    GameMatcher.WorkerShiftActive,
                    GameMatcher.View,
                    GameMatcher.Transform,
                    GameMatcher.NavigationAgent,
                    GameMatcher.CarryAnchor)
                .NoneOf(GameMatcher.Destructed));
            _products = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.EntityId,
                    GameMatcher.ProductType,
                    GameMatcher.InboundProduct,
                    GameMatcher.DeliveryEntityId,
                    GameMatcher.DeliverySlotIndex,
                    GameMatcher.Interactable,
                    GameMatcher.View,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody,
                    GameMatcher.HeldRotationOffset)
                .NoneOf(
                    GameMatcher.InStock,
                    GameMatcher.CarrierEntityId,
                    GameMatcher.LooseProduct,
                    GameMatcher.Loaded,
                    GameMatcher.TrolleyEntityId,
                    GameMatcher.TrolleySlotIndex,
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
            _tasks = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.InboundToStorageTask,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseTaskStoreEntityId,
                    GameMatcher.WarehouseTaskStorageZoneEntityId,
                    GameMatcher.WarehouseTaskProductEntityId,
                    GameMatcher.WarehouseTaskStep,
                    GameMatcher.WarehouseTaskBlockReason,
                    GameMatcher.WarehouseTaskTimeoutRemaining)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity worker in _workers)
            {
                if (worker.WarehouseWorkerStatus != WarehouseWorkerStatusId.Idle &&
                    worker.WarehouseWorkerStatus != WarehouseWorkerStatusId.StorageFull)
                    continue;
                if (_gameContext.GetEntityWithAssignedWorkerEntityId(worker.EntityId) != null)
                    throw new InvalidOperationException(
                        $"Idle warehouse worker {worker.EntityId} owns an assigned task.");

                GameEntity store = _gameContext.GetEntityWithEntityId(
                    worker.WarehouseWorkerStoreEntityId);
                if (store == null || !store.isStore || store.isDestructed ||
                    !store.hasStorageZoneEntityId)
                    throw new InvalidOperationException(
                        $"Warehouse worker {worker.EntityId} has invalid store relation.");
                if (!store.isStoreOpen)
                    continue;
                if (HasActiveTask(store.EntityId))
                    continue;

                GameEntity product = FindNextProduct(store.EntityId);
                if (product == null)
                {
                    if (worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.StorageFull)
                        worker.ReplaceWarehouseWorkerStatus(WarehouseWorkerStatusId.Idle);
                    continue;
                }

                GameEntity storageZone = _gameContext.GetEntityWithEntityId(
                    store.StorageZoneEntityId);
                if (storageZone == null || !storageZone.isStorageZone ||
                    !storageZone.hasSlots || storageZone.isDestructed)
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} has invalid storage zone relation.");

                int slotIndex = FindFreeStorageSlot(storageZone);
                if (slotIndex < 0)
                {
                    worker.ReplaceWarehouseWorkerStatus(
                        WarehouseWorkerStatusId.StorageFull);
                    continue;
                }

                _tasksFactory.CreateInboundToStorage(
                    store.EntityId,
                    product.EntityId,
                    storageZone.EntityId,
                    slotIndex);
                product.isInteractable = false;
                worker.ReplaceWarehouseWorkerStatus(WarehouseWorkerStatusId.Idle);
            }
        }

        private bool HasActiveTask(int storeEntityId)
        {
            foreach (GameEntity task in
                     _gameContext.GetEntitiesWithWarehouseTaskStoreEntityId(
                         storeEntityId))
            {
                if (task.WarehouseTaskStoreEntityId == storeEntityId &&
                    task.WarehouseTaskStep != WarehouseTaskStepId.Blocked)
                    return true;
            }

            return false;
        }

        private GameEntity FindNextProduct(int storeEntityId)
        {
            GameEntity result = null;
            foreach (GameEntity product in _products)
            {
                GameEntity delivery = _gameContext.GetEntityWithEntityId(
                    product.DeliveryEntityId);
                if (delivery == null || !delivery.isDeliveryActive ||
                    delivery.StoreEntityId != storeEntityId)
                    continue;
                if (_gameContext.GetEntityWithWarehouseTaskProductEntityId(
                        product.EntityId) != null)
                    continue;
                if (result == null ||
                    product.DeliverySlotIndex < result.DeliverySlotIndex ||
                    product.DeliverySlotIndex == result.DeliverySlotIndex &&
                    product.EntityId < result.EntityId)
                    result = product;
            }

            return result;
        }

        private int FindFreeStorageSlot(GameEntity storageZone)
        {
            _occupiedSlots.Clear();
            foreach (GameEntity product in _stockedProducts)
            {
                if (product.StorageZoneEntityId == storageZone.EntityId)
                    Reserve(_occupiedSlots, product.StorageSlotIndex, product.EntityId,
                        storageZone);
            }
            foreach (GameEntity product in _reservedStockProducts)
            {
                if (product.StorageZoneEntityId == storageZone.EntityId)
                    Reserve(_occupiedSlots, product.ReservedStorageSlotIndex,
                        product.EntityId, storageZone);
            }
            foreach (GameEntity task in
                     _gameContext.GetEntitiesWithWarehouseTaskStorageZoneEntityId(
                         storageZone.EntityId))
            {
                if (!task.isDestructed && task.isWarehouseTask &&
                    task.isInboundToStorageTask &&
                    task.hasWarehouseTaskReservedStorageSlotIndex)
                    Reserve(_occupiedSlots,
                        task.WarehouseTaskReservedStorageSlotIndex,
                        task.WarehouseTaskProductEntityId,
                        storageZone);
            }

            for (int index = 0; index < storageZone.Slots.Length; index++)
            {
                if (!_occupiedSlots.ContainsKey(index))
                    return index;
            }

            return -1;
        }

        private static void Reserve(Dictionary<int, int> occupied, int slotIndex,
            int ownerEntityId, GameEntity storageZone)
        {
            if (slotIndex < 0 || slotIndex >= storageZone.Slots.Length)
                throw new InvalidOperationException(
                    $"Entity {ownerEntityId} reserves invalid storage slot {slotIndex}.");
            if (!occupied.TryAdd(slotIndex, ownerEntityId))
                throw new InvalidOperationException(
                    $"Storage slot {slotIndex} in zone {storageZone.EntityId} is reserved twice.");
        }
    }
}
