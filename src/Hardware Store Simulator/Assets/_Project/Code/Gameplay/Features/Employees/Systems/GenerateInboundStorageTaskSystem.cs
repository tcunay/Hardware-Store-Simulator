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
                if (!store.isStoreOpen && !store.isStoreClosing)
                    continue;
                if (HasActiveTask(store.EntityId))
                    continue;

                GameEntity product = FindNextProduct(store);
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
                if (!task.isDestructed && task.isWarehouseTask &&
                    task.hasWarehouseTaskStep &&
                    task.WarehouseTaskStoreEntityId == storeEntityId &&
                    task.WarehouseTaskStep != WarehouseTaskStepId.Blocked)
                    return true;
            }

            return false;
        }

        private GameEntity FindNextProduct(GameEntity store)
        {
            GameEntity prerequisite = FindCustomerPrerequisiteProduct(store);
            if (prerequisite != null || store.isStoreClosing)
                return prerequisite;

            GameEntity result = null;
            foreach (GameEntity product in _products)
            {
                if (!IsAvailableInboundProduct(product, store.EntityId))
                    continue;
                if (result == null || CompareInboundProduct(product, result) < 0)
                    result = product;
            }

            return result;
        }

        private GameEntity FindCustomerPrerequisiteProduct(GameEntity store)
        {
            GameEntity visit = GetCurrentLoadingVisit(store);
            if (visit == null)
                return null;

            GameEntity selectedLine = null;
            GameEntity selectedProduct = null;
            foreach (GameEntity line in
                     _gameContext.GetEntitiesWithOrderEntityId(visit.EntityId))
            {
                ValidateOrderLine(visit, line);
                int linkedCount = CountLinkedProducts(line);
                int reservedCount = CountReservedProducts(line);
                if (linkedCount < line.LoadedProductCount ||
                    linkedCount + reservedCount > line.RequiredProductCount)
                {
                    throw new InvalidOperationException(
                        $"Order line {line.EntityId} has invalid linked/reserved quota.");
                }
                if (linkedCount + reservedCount >= line.RequiredProductCount ||
                    HasShelfProduct(line))
                {
                    continue;
                }

                GameEntity inbound = FindInboundProduct(
                    store.EntityId, line.ProductType);
                if (inbound == null)
                    continue;
                if (selectedLine == null ||
                    line.LineIndex < selectedLine.LineIndex ||
                    line.LineIndex == selectedLine.LineIndex &&
                    CompareInboundProduct(inbound, selectedProduct) < 0)
                {
                    selectedLine = line;
                    selectedProduct = inbound;
                }
            }

            return selectedProduct;
        }

        private GameEntity GetCurrentLoadingVisit(GameEntity store)
        {
            GameEntity bay =
                _gameContext.GetEntityWithCustomerLoadingBayStoreEntityId(
                    store.EntityId);
            if (bay == null || bay.isDestructed ||
                !bay.isCustomerLoadingBay || !bay.hasEntityId ||
                !bay.hasCustomerLoadingBayStoreEntityId ||
                bay.CustomerLoadingBayStoreEntityId != store.EntityId)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has invalid customer loading bay.");
            }
            GameEntity visit =
                _gameContext.GetEntityWithReservedCustomerLoadingBayEntityId(
                    bay.EntityId);
            if (visit == null || !visit.isCustomerVisitLoading)
                return null;
            if (visit.isDestructed || !visit.isCustomerVisit ||
                !visit.isCustomerVehicle || !visit.isOrder ||
                !visit.hasEntityId || !visit.hasCustomerVisitStoreEntityId ||
                visit.CustomerVisitStoreEntityId != store.EntityId ||
                !visit.hasStorageZoneEntityId ||
                visit.StorageZoneEntityId != store.StorageZoneEntityId ||
                !visit.hasReservedCustomerLoadingBayEntityId ||
                visit.ReservedCustomerLoadingBayEntityId != bay.EntityId)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has invalid current loading visit.");
            }
            return visit;
        }

        private GameEntity FindInboundProduct(int storeEntityId,
            ProductTypeId productType)
        {
            GameEntity selected = null;
            foreach (GameEntity product in _products)
            {
                if (product.ProductType != productType ||
                    !IsAvailableInboundProduct(product, storeEntityId))
                {
                    continue;
                }
                if (selected == null ||
                    CompareInboundProduct(product, selected) < 0)
                {
                    selected = product;
                }
            }
            return selected;
        }

        private bool IsAvailableInboundProduct(GameEntity product,
            int storeEntityId)
        {
            GameEntity delivery = _gameContext.GetEntityWithEntityId(
                product.DeliveryEntityId);
            if (delivery == null || delivery.isDestructed ||
                !delivery.isDelivery || !delivery.isDeliveryActive ||
                !delivery.hasEntityId || !delivery.hasStoreEntityId ||
                !delivery.hasSlots ||
                delivery.EntityId != product.DeliveryEntityId)
            {
                throw new InvalidOperationException(
                    $"Inbound product {product.EntityId} references invalid delivery.");
            }
            if (delivery.StoreEntityId != storeEntityId)
                return false;
            return _gameContext.GetEntityWithWarehouseTaskProductEntityId(
                product.EntityId) == null;
        }

        private bool HasShelfProduct(GameEntity line)
        {
            foreach (GameEntity product in _stockedProducts)
            {
                if (!product.isDestructed && product.isProduct &&
                    product.hasProductType &&
                    product.StorageZoneEntityId == line.StorageZoneEntityId &&
                    product.ProductType == line.ProductType)
                {
                    return true;
                }
            }
            return false;
        }

        private int CountLinkedProducts(GameEntity line)
        {
            int count = 0;
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithOrderLineEntityId(line.EntityId))
            {
                if (product.isDestructed || !product.isProduct ||
                    !product.isLoaded || !product.hasEntityId ||
                    !product.hasProductType ||
                    product.ProductType != line.ProductType ||
                    !product.hasLoadingSlotIndex)
                {
                    throw new InvalidOperationException(
                        $"Order line {line.EntityId} has an invalid linked product.");
                }
                count++;
            }
            return count;
        }

        private int CountReservedProducts(GameEntity line)
        {
            int count = 0;
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithReservedOrderLineEntityId(
                         line.EntityId))
            {
                if (product.isDestructed || !product.isProduct ||
                    !product.isInStock || !product.hasEntityId ||
                    !product.hasProductType ||
                    product.ProductType != line.ProductType ||
                    !product.hasStorageZoneEntityId ||
                    product.StorageZoneEntityId != line.StorageZoneEntityId ||
                    !product.hasReservedStorageSlotIndex)
                {
                    throw new InvalidOperationException(
                        $"Order line {line.EntityId} has an invalid product reservation.");
                }
                count++;
            }
            return count;
        }

        private static void ValidateOrderLine(GameEntity visit, GameEntity line)
        {
            if (line.isDestructed || !line.isOrderLine || !line.hasEntityId ||
                !line.hasOrderEntityId || line.OrderEntityId != visit.EntityId ||
                !line.hasStorageZoneEntityId ||
                line.StorageZoneEntityId != visit.StorageZoneEntityId ||
                !line.hasLineIndex || line.LineIndex < 0 ||
                !line.hasProductType || !line.hasRequiredProductCount ||
                !line.hasLoadedProductCount || line.RequiredProductCount <= 0 ||
                line.LoadedProductCount < 0 ||
                line.LoadedProductCount > line.RequiredProductCount)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid order line.");
            }
        }

        private static int CompareInboundProduct(GameEntity left,
            GameEntity right)
        {
            int slotComparison = left.DeliverySlotIndex.CompareTo(
                right.DeliverySlotIndex);
            return slotComparison != 0
                ? slotComparison
                : left.EntityId.CompareTo(right.EntityId);
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
