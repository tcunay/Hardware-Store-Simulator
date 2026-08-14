using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class GenerateCustomerLoadingTaskSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IWarehouseTaskFactory _tasksFactory;
        private readonly IGroup<GameEntity> _workers;
        private readonly IGroup<GameEntity> _shelfProducts;
        private readonly IGroup<GameEntity> _interactionRequests;
        private readonly Dictionary<int, int> _occupiedLoadingSlots = new();

        public GenerateCustomerLoadingTaskSystem(GameContext gameContext,
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
                    GameMatcher.CarryAnchor,
                    GameMatcher.WarehouseWorkerStoragePosition,
                    GameMatcher.WarehouseWorkerStorageRotation,
                    GameMatcher.WarehouseWorkerCustomerLoadingPosition,
                    GameMatcher.WarehouseWorkerCustomerLoadingRotation)
                .NoneOf(GameMatcher.Destructed));
            _shelfProducts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.EntityId,
                    GameMatcher.ProductType,
                    GameMatcher.InStock,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.StorageSlotIndex,
                    GameMatcher.Interactable,
                    GameMatcher.View,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody,
                    GameMatcher.HeldRotationOffset)
                .NoneOf(
                    GameMatcher.InboundProduct,
                    GameMatcher.CarrierEntityId,
                    GameMatcher.LooseProduct,
                    GameMatcher.Loaded,
                    GameMatcher.DeliveryEntityId,
                    GameMatcher.DeliverySlotIndex,
                    GameMatcher.ReservedDeliverySlotIndex,
                    GameMatcher.ReservedStorageSlotIndex,
                    GameMatcher.ReservedOrderLineEntityId,
                    GameMatcher.OrderLineEntityId,
                    GameMatcher.LoadingSlotIndex,
                    GameMatcher.TrolleyEntityId,
                    GameMatcher.TrolleySlotIndex,
                    GameMatcher.WorldPosition,
                    GameMatcher.WorldRotation,
                    GameMatcher.ProductPlacementDirty,
                    GameMatcher.Destructed));
            _interactionRequests = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.InteractionRequest,
                GameMatcher.SourceEntityId,
                GameMatcher.TargetEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity worker in _workers)
            {
                if (worker.WarehouseWorkerStatus != WarehouseWorkerStatusId.Idle &&
                    worker.WarehouseWorkerStatus !=
                    WarehouseWorkerStatusId.StorageFull)
                {
                    continue;
                }
                if (_gameContext.GetEntityWithAssignedWorkerEntityId(
                        worker.EntityId) != null)
                {
                    throw new InvalidOperationException(
                        $"Available warehouse worker {worker.EntityId} owns an assigned task.");
                }

                GameEntity store = _gameContext.GetEntityWithEntityId(
                    worker.WarehouseWorkerStoreEntityId);
                ValidateStore(worker, store);
                if (!store.isStoreOpen && !store.isStoreClosing)
                    continue;
                if (HasActiveTask(store.EntityId))
                    continue;

                GameEntity visit = GetCurrentLoadingVisit(store);
                if (visit == null)
                    continue;
                FindTaskSource(visit, store.EntityId, out GameEntity line,
                    out GameEntity product);
                if (line == null)
                    continue;

                int loadingSlotIndex = FindFreeLoadingSlot(visit);
                if (loadingSlotIndex < 0)
                {
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has an incomplete order but no " +
                        "free loading slot.");
                }

                int storageSlotIndex = product.StorageSlotIndex;
                product.RemoveStorageSlotIndex();
                product.AddReservedStorageSlotIndex(storageSlotIndex);
                product.AddReservedOrderLineEntityId(line.EntityId);
                product.isInteractable = false;

                GameEntity task = _tasksFactory.CreateStockToCustomerLoading(
                    store.EntityId,
                    product.EntityId,
                    visit.EntityId,
                    line.EntityId,
                    loadingSlotIndex);
                ValidateCreatedTask(task, store, visit, line, product,
                    loadingSlotIndex);
                if (worker.WarehouseWorkerStatus ==
                    WarehouseWorkerStatusId.StorageFull)
                {
                    worker.ReplaceWarehouseWorkerStatus(
                        WarehouseWorkerStatusId.Idle);
                }
            }
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
                !visit.hasReservedCustomerLoadingBayEntityId ||
                visit.ReservedCustomerLoadingBayEntityId != bay.EntityId ||
                !visit.hasStorageZoneEntityId ||
                visit.StorageZoneEntityId != store.StorageZoneEntityId ||
                !visit.hasSlots)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has invalid current loading visit.");
            }
            return visit;
        }

        private void FindTaskSource(GameEntity visit, int storeEntityId,
            out GameEntity selectedLine, out GameEntity selectedProduct)
        {
            selectedLine = null;
            selectedProduct = null;
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
                        $"Order line {line.EntityId} has invalid loaded/reserved quota.");
                }
                if (linkedCount + reservedCount >= line.RequiredProductCount)
                    continue;

                GameEntity product = FindShelfProduct(line, storeEntityId);
                if (product == null)
                    continue;
                if (selectedLine == null ||
                    line.LineIndex < selectedLine.LineIndex ||
                    line.LineIndex == selectedLine.LineIndex &&
                    CompareShelfProduct(product, selectedProduct) < 0)
                {
                    selectedLine = line;
                    selectedProduct = product;
                }
            }
        }

        private GameEntity FindShelfProduct(GameEntity line, int storeEntityId)
        {
            GameEntity selected = null;
            foreach (GameEntity product in _shelfProducts)
            {
                if (product.StorageZoneEntityId != line.StorageZoneEntityId ||
                    product.ProductType != line.ProductType ||
                    _gameContext.GetEntityWithWarehouseTaskProductEntityId(
                        product.EntityId) != null ||
                    HasValidPlayerRequest(product.EntityId, storeEntityId))
                {
                    continue;
                }

                if (selected == null || CompareShelfProduct(product, selected) < 0)
                    selected = product;
            }
            return selected;
        }

        private bool HasValidPlayerRequest(int productEntityId, int storeEntityId)
        {
            foreach (GameEntity request in _interactionRequests)
            {
                if (request.TargetEntityId != productEntityId)
                    continue;
                GameEntity source = _gameContext.GetEntityWithEntityId(
                    request.SourceEntityId);
                if (source != null && !source.isDestructed && source.isPlayer &&
                    source.hasEntityId && source.hasStoreEntityId &&
                    source.StoreEntityId == storeEntityId)
                {
                    return true;
                }
            }
            return false;
        }

        private int FindFreeLoadingSlot(GameEntity visit)
        {
            _occupiedLoadingSlots.Clear();
            foreach (GameEntity line in
                     _gameContext.GetEntitiesWithOrderEntityId(visit.EntityId))
            {
                ValidateOrderLine(visit, line);
                foreach (GameEntity product in
                         _gameContext.GetEntitiesWithOrderLineEntityId(line.EntityId))
                {
                    ValidateLoadedProduct(line, product);
                    ReserveLoadingSlot(visit, product.LoadingSlotIndex,
                        product.EntityId);
                }
            }
            foreach (GameEntity task in
                     _gameContext.GetEntitiesWithWarehouseTaskCustomerVisitEntityId(
                         visit.EntityId))
            {
                if (task.isDestructed ||
                    !task.hasWarehouseTaskReservedLoadingSlotIndex)
                {
                    continue;
                }
                if (!task.isWarehouseTask ||
                    !task.isStockToCustomerLoadingTask ||
                    task.isInboundToStorageTask || !task.hasEntityId ||
                    !task.hasWarehouseTaskStep)
                {
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has invalid task reservation.");
                }
                ReserveLoadingSlot(visit,
                    task.WarehouseTaskReservedLoadingSlotIndex,
                    task.EntityId);
            }

            for (int slotIndex = 0; slotIndex < visit.Slots.Length; slotIndex++)
            {
                if (!_occupiedLoadingSlots.ContainsKey(slotIndex))
                    return slotIndex;
            }
            return -1;
        }

        private void ReserveLoadingSlot(GameEntity visit, int slotIndex,
            int ownerEntityId)
        {
            if (slotIndex < 0 || slotIndex >= visit.Slots.Length)
            {
                throw new InvalidOperationException(
                    $"Entity {ownerEntityId} reserves invalid loading slot " +
                    $"{slotIndex} for visit {visit.EntityId}.");
            }
            if (!_occupiedLoadingSlots.TryAdd(slotIndex, ownerEntityId))
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} loading slot {slotIndex} is " +
                    "occupied or reserved twice.");
            }
        }

        private int CountLinkedProducts(GameEntity line)
        {
            int count = 0;
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithOrderLineEntityId(line.EntityId))
            {
                ValidateLoadedProduct(line, product);
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

        private static void ValidateLoadedProduct(GameEntity line,
            GameEntity product)
        {
            if (product.isDestructed || !product.isProduct ||
                !product.isLoaded || !product.hasEntityId ||
                !product.hasProductType ||
                product.ProductType != line.ProductType ||
                !product.hasOrderLineEntityId ||
                product.OrderLineEntityId != line.EntityId ||
                !product.hasLoadingSlotIndex)
            {
                throw new InvalidOperationException(
                    $"Order line {line.EntityId} has an invalid loaded product.");
            }
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

        private static int CompareShelfProduct(GameEntity left,
            GameEntity right)
        {
            int slotComparison = left.StorageSlotIndex.CompareTo(
                right.StorageSlotIndex);
            return slotComparison != 0
                ? slotComparison
                : left.EntityId.CompareTo(right.EntityId);
        }

        private bool HasActiveTask(int storeEntityId)
        {
            foreach (GameEntity task in
                     _gameContext.GetEntitiesWithWarehouseTaskStoreEntityId(
                         storeEntityId))
            {
                if (!task.isDestructed && task.isWarehouseTask &&
                    task.hasWarehouseTaskStep &&
                    task.WarehouseTaskStep != WarehouseTaskStepId.Blocked)
                {
                    return true;
                }
            }
            return false;
        }

        private static void ValidateCreatedTask(GameEntity task, GameEntity store,
            GameEntity visit, GameEntity line, GameEntity product,
            int loadingSlotIndex)
        {
            if (task == null || task.isDestructed || !task.isWarehouseTask ||
                !task.isStockToCustomerLoadingTask ||
                task.isInboundToStorageTask || !task.hasEntityId ||
                !task.hasWarehouseTaskStoreEntityId ||
                task.WarehouseTaskStoreEntityId != store.EntityId ||
                !task.hasWarehouseTaskProductEntityId ||
                task.WarehouseTaskProductEntityId != product.EntityId ||
                !task.hasWarehouseTaskCustomerVisitEntityId ||
                task.WarehouseTaskCustomerVisitEntityId != visit.EntityId ||
                !task.hasWarehouseTaskOrderLineEntityId ||
                task.WarehouseTaskOrderLineEntityId != line.EntityId ||
                !task.hasWarehouseTaskReservedLoadingSlotIndex ||
                task.WarehouseTaskReservedLoadingSlotIndex != loadingSlotIndex ||
                task.hasWarehouseTaskStorageZoneEntityId ||
                task.hasWarehouseTaskReservedStorageSlotIndex ||
                !task.hasWarehouseTaskStep ||
                task.WarehouseTaskStep != WarehouseTaskStepId.Available ||
                !task.hasWarehouseTaskBlockReason ||
                task.WarehouseTaskBlockReason != WarehouseTaskBlockReasonId.None ||
                !task.hasWarehouseTaskTimeoutRemaining ||
                task.hasAssignedWorkerEntityId)
            {
                throw new InvalidOperationException(
                    "Warehouse task factory returned invalid customer loading task.");
            }
        }

        private static void ValidateStore(GameEntity worker, GameEntity store)
        {
            if (store == null || store.isDestructed || !store.isStore ||
                !store.hasEntityId || !store.hasStorageZoneEntityId)
            {
                throw new InvalidOperationException(
                    $"Warehouse worker {worker.EntityId} has invalid store relation.");
            }
            int phaseCount =
                (store.isStorePreparing ? 1 : 0) +
                (store.isStoreOpen ? 1 : 0) +
                (store.isStoreClosing ? 1 : 0) +
                (store.isDayReportOpen ? 1 : 0);
            if (phaseCount != 1)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} must have exactly one day phase.");
            }
        }
    }
}
