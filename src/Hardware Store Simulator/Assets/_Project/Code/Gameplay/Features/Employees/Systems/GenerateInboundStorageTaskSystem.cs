using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common;
using HardwareStore.Gameplay.Common.Navigation;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Features.Employees;
using HardwareStore.Gameplay.Scene;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class GenerateInboundStorageTaskSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IWarehouseTaskFactory _tasksFactory;
        private readonly IStoreSceneData _sceneData;
        private readonly IWorkerNavigationService _navigation;
        private readonly WarehouseWorkerConfig _workerConfig;
        private readonly IGroup<GameEntity> _workers;
        private readonly IGroup<GameEntity> _products;
        private readonly IGroup<GameEntity> _stockedProducts;
        private readonly IGroup<GameEntity> _reservedStockProducts;
        private readonly IGroup<GameEntity> _tasks;
        private readonly Dictionary<int, int> _occupiedSlots = new();
        private readonly List<int> _freeStorageSlots = new(18);
        private readonly List<GameEntity> _batchProducts = new(3);
        private readonly List<GameEntity> _candidateProducts = new(18);

        public GenerateInboundStorageTaskSystem(GameContext gameContext,
            IWarehouseTaskFactory tasksFactory, IStoreSceneData sceneData,
            IWorkerNavigationService navigation, IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _tasksFactory = tasksFactory;
            _sceneData = sceneData;
            _navigation = navigation;
            _workerConfig = staticData.WarehouseWorker;
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
                    GameMatcher.PurchaseOrderLineEntityId,
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
                bool returning = worker.WarehouseWorkerStatus ==
                    WarehouseWorkerStatusId.ReturningWorkerTrolley;
                if (!returning &&
                    worker.WarehouseWorkerStatus != WarehouseWorkerStatusId.Idle &&
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

                GameEntity storageZone = _gameContext.GetEntityWithEntityId(
                    store.StorageZoneEntityId);
                if (storageZone == null || !storageZone.isStorageZone ||
                    !storageZone.hasSlots || storageZone.isDestructed)
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} has invalid storage zone relation.");

                GameEntity trolley = returning
                    ? GetLeasedWorkerTrolley(worker, store)
                    : TryGetAvailablePlatformTrolley(worker, store);
                int capacity = trolley?.TrolleyCapacity ?? 1;
                CollectBatchProducts(store, capacity);
                if (_batchProducts.Count == 0)
                {
                    if (!returning && worker.WarehouseWorkerStatus ==
                        WarehouseWorkerStatusId.StorageFull)
                        worker.ReplaceWarehouseWorkerStatus(
                            WarehouseWorkerStatusId.Idle);
                    continue;
                }

                BuildFreeStorageSlots(storageZone);
                int productCount = Math.Min(_batchProducts.Count,
                    _freeStorageSlots.Count);
                if (productCount == 0)
                {
                    if (!returning)
                    {
                        worker.ReplaceWarehouseWorkerStatus(
                            WarehouseWorkerStatusId.StorageFull);
                    }
                    continue;
                }

                if (trolley != null)
                {
                    if (!returning)
                    {
                        WorkerTrolleyLeaseUtility.BeginLease(
                            trolley,
                            store.EntityId,
                            _sceneData.GetSpawnPoint(
                                SpawnPointId.WarehouseWorkerTrolleyCustomerLoadingAccess));
                    }
                    CreateTrolleyBatch(worker, store, storageZone, trolley,
                        productCount, returning);
                }
                else if (!returning)
                {
                    CreateManualTask(worker, store, storageZone,
                        _batchProducts[0], _freeStorageSlots[0]);
                }
            }
        }

        private void CreateTrolleyBatch(GameEntity worker, GameEntity store,
            GameEntity storageZone, GameEntity trolley, int productCount,
            bool returning)
        {
            if (productCount < 1 || productCount > trolley.TrolleyCapacity ||
                trolley.OccupiedTrolleySlotCount != 0 ||
                _gameContext.GetEntitiesWithWorkerTrolleyEntityId(
                    trolley.EntityId).Count != 0 ||
                _gameContext.GetEntityWithWarehouseTaskWorkerTrolleyEntityId(
                    trolley.EntityId) != null)
            {
                throw new InvalidOperationException(
                    $"Worker trolley {trolley.EntityId} cannot start an inbound batch.");
            }
            if (returning)
            {
                if (!trolley.hasTrolleyPusherEntityId ||
                    trolley.TrolleyPusherEntityId != worker.EntityId ||
                    !worker.isPushingWorkerTrolley || !worker.isHandsOccupied)
                    throw new InvalidOperationException(
                        $"Returning worker {worker.EntityId} lost trolley ownership.");
            }
            else if (trolley.hasTrolleyPusherEntityId ||
                     worker.isPushingWorkerTrolley || worker.isHandsOccupied)
            {
                throw new InvalidOperationException(
                    $"Idle worker {worker.EntityId} cannot start an inbound trolley run.");
            }

            GameEntity primary = _tasksFactory.CreateWorkerTrolleyInboundStorageRun(
                store.EntityId,
                _batchProducts[0].EntityId,
                storageZone.EntityId,
                _freeStorageSlots[0],
                trolley.EntityId,
                productCount);
            for (int index = 1; index < productCount; index++)
            {
                _tasksFactory.CreateInboundToStorage(
                    store.EntityId,
                    _batchProducts[index].EntityId,
                    storageZone.EntityId,
                    _freeStorageSlots[index]);
            }
            for (int index = 0; index < productCount; index++)
            {
                GameEntity product = _batchProducts[index];
                product.AddWarehouseRunEntityId(primary.EntityId);
                product.isInteractable = false;
            }
            ValidateCreatedBatch(primary, trolley, storageZone, productCount);
            if (worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.StorageFull)
                worker.ReplaceWarehouseWorkerStatus(WarehouseWorkerStatusId.Idle);
        }

        private void CreateManualTask(GameEntity worker, GameEntity store,
            GameEntity storageZone, GameEntity product, int storageSlotIndex)
        {
            _tasksFactory.CreateInboundToStorage(
                store.EntityId, product.EntityId,
                storageZone.EntityId, storageSlotIndex);
            product.isInteractable = false;
            worker.ReplaceWarehouseWorkerStatus(WarehouseWorkerStatusId.Idle);
        }

        private void CollectBatchProducts(GameEntity store, int capacity)
        {
            if (capacity <= 0)
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid inbound batch capacity.");
            _batchProducts.Clear();
            GameEntity first = FindNextProduct(store);
            if (first == null)
                return;
            _batchProducts.Add(first);
            if (capacity == 1 || store.isStoreClosing)
                return;

            _candidateProducts.Clear();
            foreach (GameEntity product in _products)
            {
                if (product != first &&
                    IsAvailableInboundProduct(product, store.EntityId))
                    _candidateProducts.Add(product);
            }
            _candidateProducts.Sort(CompareInboundProduct);
            foreach (GameEntity product in _candidateProducts)
            {
                if (_batchProducts.Count == capacity)
                    break;
                _batchProducts.Add(product);
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
            InboundProductManifestValidator.Validate(_gameContext, product);
            GameEntity delivery = _gameContext.GetEntityWithEntityId(
                product.DeliveryEntityId);
            if (delivery == null || delivery.isDestructed ||
                !delivery.isDelivery || !delivery.isDeliveryActive ||
                !delivery.hasEntityId || !delivery.hasStoreEntityId ||
                !delivery.hasSlots || !delivery.hasDeliveryPurchaseOrderEntityId ||
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

        private void BuildFreeStorageSlots(GameEntity storageZone)
        {
            _occupiedSlots.Clear();
            _freeStorageSlots.Clear();
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
                    _freeStorageSlots.Add(index);
            }
        }

        private GameEntity TryGetAvailablePlatformTrolley(GameEntity worker,
            GameEntity store)
        {
            GameEntity trolley =
                _gameContext.GetEntityWithTrolleyStoreEntityId(store.EntityId);
            if (trolley == null || trolley.isDestructed)
                return null;
            ValidatePlatformTrolley(store, trolley);
            if (trolley.isWorkerTrolley || !trolley.isInteractable ||
                trolley.hasTrolleyPusherEntityId ||
                trolley.OccupiedTrolleySlotCount != 0 ||
                _gameContext.GetEntitiesWithTrolleyEntityId(
                    trolley.EntityId).Count != 0 ||
                _gameContext.GetEntitiesWithWorkerTrolleyEntityId(
                    trolley.EntityId).Count != 0 ||
                _gameContext.GetEntityWithWarehouseTaskWorkerTrolleyEntityId(
                    trolley.EntityId) != null)
            {
                return null;
            }

            Pose homePose = new(trolley.Transform.position,
                trolley.Transform.rotation);
            return _navigation.CanReach(
                worker.NavigationAgent,
                WorkerTrolleyLeaseUtility.GetPusherPosition(trolley, homePose),
                _workerConfig.NavigationSampleRadius)
                ? trolley
                : null;
        }

        private GameEntity GetLeasedWorkerTrolley(GameEntity worker,
            GameEntity store)
        {
            GameEntity trolley =
                _gameContext.GetEntityWithWorkerTrolleyStoreEntityId(store.EntityId);
            if (trolley == null || trolley.isDestructed)
                return null;
            ValidatePlatformTrolley(store, trolley);
            if (!trolley.isWorkerTrolley || trolley.isInteractable ||
                !trolley.hasWorkerTrolleyStoreEntityId ||
                trolley.WorkerTrolleyStoreEntityId != store.EntityId ||
                !trolley.hasWorkerTrolleyHomePosition ||
                !trolley.hasWorkerTrolleyHomeRotation ||
                !trolley.hasWorkerTrolleyCustomerLoadingPosition ||
                !trolley.hasWorkerTrolleyCustomerLoadingRotation ||
                !trolley.hasTrolleyPusherEntityId ||
                trolley.TrolleyPusherEntityId != worker.EntityId ||
                trolley.OccupiedTrolleySlotCount != 0 ||
                _gameContext.GetEntitiesWithTrolleyEntityId(
                    trolley.EntityId).Count != 0 ||
                _gameContext.GetEntitiesWithWorkerTrolleyEntityId(
                    trolley.EntityId).Count != 0 ||
                _gameContext.GetEntityWithWarehouseTaskWorkerTrolleyEntityId(
                    trolley.EntityId) != null)
            {
                throw new InvalidOperationException(
                    $"Returning worker {worker.EntityId} has an invalid leased trolley.");
            }
            return trolley;
        }

        private static void ValidatePlatformTrolley(GameEntity store,
            GameEntity trolley)
        {
            if (trolley.isDestructed || !trolley.isPlatformTrolley ||
                !trolley.hasEntityId || !trolley.hasTrolleyStoreEntityId ||
                trolley.TrolleyStoreEntityId != store.EntityId ||
                !trolley.hasTrolleyCapacity || trolley.TrolleyCapacity <= 0 ||
                !trolley.hasOccupiedTrolleySlotCount ||
                trolley.OccupiedTrolleySlotCount < 0 ||
                trolley.OccupiedTrolleySlotCount > trolley.TrolleyCapacity ||
                !trolley.hasTrolleyMovementSpeed ||
                !trolley.hasTrolleyFollowDistance ||
                !trolley.hasView || !trolley.hasTransform ||
                !trolley.hasRigidbody || !trolley.hasColliders ||
                !trolley.hasSlots ||
                trolley.Slots.Length != trolley.TrolleyCapacity)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid platform trolley.");
            }
        }

        private void ValidateCreatedBatch(GameEntity primary,
            GameEntity trolley, GameEntity storageZone, int productCount)
        {
            if (primary == null || primary.isDestructed ||
                !primary.isWarehouseTask || !primary.isInboundToStorageTask ||
                !primary.isWorkerTrolleyInboundStorageRun ||
                !primary.hasEntityId ||
                !primary.hasWarehouseTaskWorkerTrolleyEntityId ||
                primary.WarehouseTaskWorkerTrolleyEntityId != trolley.EntityId ||
                !primary.hasWarehouseRunProductCount ||
                primary.WarehouseRunProductCount != productCount)
            {
                throw new InvalidOperationException(
                    "Inbound worker-trolley primary task was created incorrectly.");
            }

            for (int index = 0; index < productCount; index++)
            {
                GameEntity product = _batchProducts[index];
                GameEntity task =
                    _gameContext.GetEntityWithWarehouseTaskProductEntityId(
                        product.EntityId);
                if (task == null || task.isDestructed ||
                    !task.isWarehouseTask || !task.isInboundToStorageTask ||
                    !task.hasWarehouseTaskStorageZoneEntityId ||
                    task.WarehouseTaskStorageZoneEntityId != storageZone.EntityId ||
                    !task.hasWarehouseTaskReservedStorageSlotIndex ||
                    task.WarehouseTaskReservedStorageSlotIndex !=
                    _freeStorageSlots[index] ||
                    !product.hasWarehouseRunEntityId ||
                    product.WarehouseRunEntityId != primary.EntityId ||
                    product.isInteractable ||
                    (index == 0) != (task == primary) ||
                    (index == 0) != task.isWorkerTrolleyInboundStorageRun)
                {
                    throw new InvalidOperationException(
                        $"Inbound trolley product {product.EntityId} has an invalid task.");
                }
            }
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
