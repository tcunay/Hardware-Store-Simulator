using System;
using System.Collections.Generic;
using Entitas;
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
    public sealed class GenerateCustomerLoadingTaskSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IWarehouseTaskFactory _tasksFactory;
        private readonly IStoreSceneData _sceneData;
        private readonly IWorkerNavigationService _navigation;
        private readonly WarehouseWorkerConfig _workerConfig;
        private readonly IGroup<GameEntity> _workers;
        private readonly IGroup<GameEntity> _shelfProducts;
        private readonly IGroup<GameEntity> _interactionRequests;
        private readonly Dictionary<int, int> _occupiedLoadingSlots = new();
        private readonly List<int> _freeLoadingSlots = new(8);
        private readonly List<GameEntity> _orderLines = new(8);
        private readonly List<GameEntity> _matchingProducts = new(16);
        private readonly List<BatchCandidate> _batchCandidates = new(3);
        private readonly HashSet<int> _selectedProductIds = new();

        public GenerateCustomerLoadingTaskSystem(GameContext gameContext,
            IWarehouseTaskFactory tasksFactory, IStoreSceneData sceneData,
            IWorkerNavigationService navigation, IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _tasksFactory = tasksFactory;
            _sceneData = sceneData;
            _navigation = navigation;
            _workerConfig = staticData.WarehouseWorker;
            _workers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseWorker, GameMatcher.EntityId,
                    GameMatcher.WarehouseWorkerStoreEntityId,
                    GameMatcher.WarehouseWorkerStatus,
                    GameMatcher.WorkerShiftActive, GameMatcher.View,
                    GameMatcher.Transform, GameMatcher.NavigationAgent,
                    GameMatcher.CarryAnchor,
                    GameMatcher.WarehouseWorkerStoragePosition,
                    GameMatcher.WarehouseWorkerStorageRotation,
                    GameMatcher.WarehouseWorkerCustomerLoadingPosition,
                    GameMatcher.WarehouseWorkerCustomerLoadingRotation)
                .NoneOf(GameMatcher.Destructed));
            _shelfProducts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product, GameMatcher.EntityId,
                    GameMatcher.ProductType, GameMatcher.InStock,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.StorageSlotIndex, GameMatcher.Interactable,
                    GameMatcher.View, GameMatcher.Transform,
                    GameMatcher.Rigidbody, GameMatcher.HeldRotationOffset)
                .NoneOf(
                    GameMatcher.InboundProduct, GameMatcher.CarrierEntityId,
                    GameMatcher.LooseProduct, GameMatcher.Loaded,
                    GameMatcher.DeliveryEntityId, GameMatcher.DeliverySlotIndex,
                    GameMatcher.ReservedDeliverySlotIndex,
                    GameMatcher.ReservedStorageSlotIndex,
                    GameMatcher.ReservedOrderLineEntityId,
                    GameMatcher.ReservedCustomerLoadingSlotIndex,
                    GameMatcher.WarehouseRunEntityId,
                    GameMatcher.OrderLineEntityId, GameMatcher.LoadingSlotIndex,
                    GameMatcher.TrolleyEntityId, GameMatcher.TrolleySlotIndex,
                    GameMatcher.WorkerTrolleyEntityId,
                    GameMatcher.WorkerTrolleySlotIndex,
                    GameMatcher.WorldPosition, GameMatcher.WorldRotation,
                    GameMatcher.ProductPlacementDirty, GameMatcher.Destructed));
            _interactionRequests = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.InteractionRequest, GameMatcher.SourceEntityId,
                GameMatcher.TargetEntityId));
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
                if (_gameContext.GetEntityWithAssignedWorkerEntityId(
                        worker.EntityId) != null)
                {
                    throw new InvalidOperationException(
                        $"Available warehouse worker {worker.EntityId} owns an assigned task.");
                }

                GameEntity store = _gameContext.GetEntityWithEntityId(
                    worker.WarehouseWorkerStoreEntityId);
                ValidateStore(worker, store);
                if ((!store.isStoreOpen && !store.isStoreClosing) ||
                    HasActiveTask(store.EntityId))
                    continue;
                GameEntity visit = GetCurrentLoadingVisit(store);
                if (visit == null)
                    continue;

                GameEntity trolley = returning
                    ? GetLeasedWorkerTrolley(worker, store)
                    : TryGetAvailablePlatformTrolley(worker, store);
                CollectBatchCandidates(visit, store.EntityId,
                    trolley?.TrolleyCapacity ?? 1);
                if (_batchCandidates.Count == 0)
                    continue;
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
                    CreateBatchRun(worker, store, visit, trolley, returning);
                }
                else if (!returning)
                {
                    CreateDirectTask(worker, store, visit, _batchCandidates[0]);
                }
            }
        }

        private void CreateBatchRun(GameEntity worker, GameEntity store,
            GameEntity visit, GameEntity trolley, bool returning)
        {
            if (trolley.OccupiedTrolleySlotCount != 0 ||
                _gameContext.GetEntitiesWithWorkerTrolleyEntityId(
                    trolley.EntityId).Count != 0 ||
                _gameContext.GetEntityWithWarehouseTaskWorkerTrolleyEntityId(
                    trolley.EntityId) != null)
            {
                throw new InvalidOperationException(
                    $"Worker trolley {trolley.EntityId} is not empty and available.");
            }
            if (returning)
            {
                if (!trolley.hasTrolleyPusherEntityId ||
                    trolley.TrolleyPusherEntityId != worker.EntityId ||
                    !worker.isPushingWorkerTrolley || !worker.isHandsOccupied)
                {
                    throw new InvalidOperationException(
                        $"Returning worker {worker.EntityId} has invalid trolley ownership.");
                }
            }
            else if (trolley.hasTrolleyPusherEntityId ||
                     worker.isPushingWorkerTrolley || worker.isHandsOccupied)
            {
                throw new InvalidOperationException(
                    $"Idle worker {worker.EntityId} cannot start a trolley run while occupied.");
            }

            GameEntity run = _tasksFactory.CreateWorkerTrolleyCustomerLoadingRun(
                store.EntityId, visit.EntityId, trolley.EntityId);
            run.AddWarehouseRunProductCount(_batchCandidates.Count);
            foreach (BatchCandidate candidate in _batchCandidates)
            {
                GameEntity product = candidate.Product;
                int storageSlotIndex = product.StorageSlotIndex;
                product.RemoveStorageSlotIndex();
                product.AddReservedStorageSlotIndex(storageSlotIndex);
                product.AddReservedOrderLineEntityId(candidate.Line.EntityId);
                product.AddReservedCustomerLoadingSlotIndex(
                    candidate.LoadingSlotIndex);
                product.AddWarehouseRunEntityId(run.EntityId);
                product.isInteractable = false;
            }
            ValidateCreatedRun(run, store, visit, trolley,
                _batchCandidates.Count);
            if (worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.StorageFull)
                worker.ReplaceWarehouseWorkerStatus(WarehouseWorkerStatusId.Idle);
        }

        private void CreateDirectTask(GameEntity worker, GameEntity store,
            GameEntity visit, BatchCandidate candidate)
        {
            GameEntity product = candidate.Product;
            int storageSlotIndex = product.StorageSlotIndex;
            product.RemoveStorageSlotIndex();
            product.AddReservedStorageSlotIndex(storageSlotIndex);
            product.AddReservedOrderLineEntityId(candidate.Line.EntityId);
            product.isInteractable = false;
            GameEntity task = _tasksFactory.CreateStockToCustomerLoading(
                store.EntityId, product.EntityId, visit.EntityId,
                candidate.Line.EntityId, candidate.LoadingSlotIndex);
            ValidateCreatedDirectTask(task, store, visit, candidate);
            if (worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.StorageFull)
                worker.ReplaceWarehouseWorkerStatus(WarehouseWorkerStatusId.Idle);
        }

        private void CollectBatchCandidates(GameEntity visit,
            int storeEntityId, int capacity)
        {
            BuildFreeLoadingSlots(visit);
            _batchCandidates.Clear();
            _selectedProductIds.Clear();
            _orderLines.Clear();
            foreach (GameEntity line in
                     _gameContext.GetEntitiesWithOrderEntityId(visit.EntityId))
            {
                ValidateOrderLine(visit, line);
                _orderLines.Add(line);
            }
            _orderLines.Sort(CompareOrderLines);

            foreach (GameEntity line in _orderLines)
            {
                int linkedCount = CountLinkedProducts(line);
                int reservedCount = CountReservedProducts(line);
                if (linkedCount < line.LoadedProductCount ||
                    linkedCount + reservedCount > line.RequiredProductCount)
                {
                    throw new InvalidOperationException(
                        $"Order line {line.EntityId} has invalid loaded/reserved quota.");
                }
                int remaining = line.RequiredProductCount - linkedCount - reservedCount;
                int playerClaimCount = ClaimPendingPlayerRequests(
                    line, storeEntityId, remaining);
                remaining -= playerClaimCount;
                if (remaining <= 0)
                    continue;

                _matchingProducts.Clear();
                foreach (GameEntity product in _shelfProducts)
                {
                    if (product.StorageZoneEntityId == line.StorageZoneEntityId &&
                        product.ProductType == line.ProductType &&
                        _gameContext.GetEntityWithWarehouseTaskProductEntityId(
                            product.EntityId) == null &&
                        !_selectedProductIds.Contains(product.EntityId) &&
                        !HasValidPlayerRequest(product.EntityId, storeEntityId))
                        _matchingProducts.Add(product);
                }
                _matchingProducts.Sort(CompareShelfProduct);
                foreach (GameEntity product in _matchingProducts)
                {
                    if (remaining == 0 || _batchCandidates.Count == capacity ||
                        _batchCandidates.Count == _freeLoadingSlots.Count)
                        break;
                    _batchCandidates.Add(new BatchCandidate(
                        line, product, _freeLoadingSlots[_batchCandidates.Count]));
                    _selectedProductIds.Add(product.EntityId);
                    remaining--;
                }
                if (_batchCandidates.Count == capacity ||
                    _batchCandidates.Count == _freeLoadingSlots.Count)
                    break;
            }
        }

        private void BuildFreeLoadingSlots(GameEntity visit)
        {
            _occupiedLoadingSlots.Clear();
            _freeLoadingSlots.Clear();
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
                foreach (GameEntity product in
                         _gameContext.GetEntitiesWithReservedOrderLineEntityId(
                             line.EntityId))
                {
                    if (!product.hasReservedCustomerLoadingSlotIndex)
                        continue;
                    ValidateBatchReservedProduct(line, product);
                    ReserveLoadingSlot(visit,
                        product.ReservedCustomerLoadingSlotIndex,
                        product.EntityId);
                }
            }
            foreach (GameEntity task in
                     _gameContext.GetEntitiesWithWarehouseTaskCustomerVisitEntityId(
                         visit.EntityId))
            {
                if (task.isDestructed ||
                    !task.hasWarehouseTaskReservedLoadingSlotIndex)
                    continue;
                if (!task.isWarehouseTask ||
                    !task.isStockToCustomerLoadingTask ||
                    task.isInboundToStorageTask ||
                    task.isWorkerTrolleyCustomerLoadingRun ||
                    !task.hasEntityId || !task.hasWarehouseTaskStep)
                {
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has invalid task reservation.");
                }
                ReserveLoadingSlot(visit,
                    task.WarehouseTaskReservedLoadingSlotIndex, task.EntityId);
            }
            for (int slotIndex = 0; slotIndex < visit.Slots.Length; slotIndex++)
            {
                if (!_occupiedLoadingSlots.ContainsKey(slotIndex))
                    _freeLoadingSlots.Add(slotIndex);
            }
        }

        private GameEntity GetCurrentLoadingVisit(GameEntity store)
        {
            GameEntity bay =
                _gameContext.GetEntityWithCustomerLoadingBayStoreEntityId(
                    store.EntityId);
            if (bay == null || bay.isDestructed || !bay.isCustomerLoadingBay ||
                !bay.hasEntityId || !bay.hasCustomerLoadingBayStoreEntityId ||
                bay.CustomerLoadingBayStoreEntityId != store.EntityId)
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has invalid customer loading bay.");
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
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has invalid current loading visit.");
            return visit;
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
                return null;
            Pose homePose = new(trolley.Transform.position,
                trolley.Transform.rotation);
            if (!_navigation.CanReach(worker.NavigationAgent,
                    WorkerTrolleyLeaseUtility.GetPusherPosition(trolley, homePose),
                    _workerConfig.NavigationSampleRadius))
            {
                return null;
            }
            return trolley;
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
                throw new InvalidOperationException(
                    $"Returning worker {worker.EntityId} has an invalid leased trolley.");
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
                !trolley.hasSlots || trolley.Slots.Length != trolley.TrolleyCapacity)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid platform trolley.");
            }
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
                    source.StoreEntityId == storeEntityId &&
                    !source.isHandsOccupied)
                    return true;
            }
            return false;
        }

        private int ClaimPendingPlayerRequests(GameEntity line,
            int storeEntityId, int maximumClaimCount)
        {
            int claimCount = 0;
            if (maximumClaimCount <= 0)
                return claimCount;
            foreach (GameEntity product in _shelfProducts)
            {
                if (claimCount == maximumClaimCount)
                    break;
                if (product.StorageZoneEntityId != line.StorageZoneEntityId ||
                    product.ProductType != line.ProductType ||
                    _selectedProductIds.Contains(product.EntityId) ||
                    _gameContext.GetEntityWithWarehouseTaskProductEntityId(
                        product.EntityId) != null ||
                    !HasValidPlayerRequest(product.EntityId, storeEntityId))
                    continue;
                _selectedProductIds.Add(product.EntityId);
                claimCount++;
            }
            return claimCount;
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
                     _gameContext.GetEntitiesWithReservedOrderLineEntityId(line.EntityId))
            {
                if (product.isDestructed || !product.isProduct ||
                    !product.isInStock || !product.hasEntityId ||
                    !product.hasProductType || product.ProductType != line.ProductType ||
                    !product.hasStorageZoneEntityId ||
                    product.StorageZoneEntityId != line.StorageZoneEntityId ||
                    !product.hasReservedStorageSlotIndex)
                    throw new InvalidOperationException(
                        $"Order line {line.EntityId} has an invalid product reservation.");
                count++;
            }
            return count;
        }

        private static void ValidateBatchReservedProduct(GameEntity line,
            GameEntity product)
        {
            if (product.isDestructed || !product.isProduct ||
                !product.isInStock || product.isInboundProduct || product.isLoaded ||
                product.isInteractable || !product.hasEntityId ||
                !product.hasProductType || product.ProductType != line.ProductType ||
                !product.hasStorageZoneEntityId ||
                product.StorageZoneEntityId != line.StorageZoneEntityId ||
                !product.hasReservedStorageSlotIndex ||
                !product.hasReservedOrderLineEntityId ||
                product.ReservedOrderLineEntityId != line.EntityId ||
                !product.hasReservedCustomerLoadingSlotIndex ||
                !product.hasWarehouseRunEntityId)
                throw new InvalidOperationException(
                    $"Order line {line.EntityId} has an invalid batch reservation.");
        }

        private static void ValidateLoadedProduct(GameEntity line,
            GameEntity product)
        {
            if (product.isDestructed || !product.isProduct || !product.isLoaded ||
                !product.hasEntityId || !product.hasProductType ||
                product.ProductType != line.ProductType ||
                !product.hasOrderLineEntityId ||
                product.OrderLineEntityId != line.EntityId ||
                !product.hasLoadingSlotIndex)
                throw new InvalidOperationException(
                    $"Order line {line.EntityId} has an invalid loaded product.");
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
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid order line.");
        }

        private void ReserveLoadingSlot(GameEntity visit, int slotIndex,
            int ownerEntityId)
        {
            if (slotIndex < 0 || slotIndex >= visit.Slots.Length)
                throw new InvalidOperationException(
                    $"Entity {ownerEntityId} reserves invalid loading slot {slotIndex} " +
                    $"for visit {visit.EntityId}.");
            if (!_occupiedLoadingSlots.TryAdd(slotIndex, ownerEntityId))
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} loading slot {slotIndex} " +
                    "is occupied or reserved twice.");
        }

        private bool HasActiveTask(int storeEntityId)
        {
            foreach (GameEntity task in
                     _gameContext.GetEntitiesWithWarehouseTaskStoreEntityId(storeEntityId))
            {
                if (!task.isDestructed && task.isWarehouseTask &&
                    task.hasWarehouseTaskStep &&
                    task.WarehouseTaskStep != WarehouseTaskStepId.Blocked)
                    return true;
            }
            return false;
        }

        private static void ValidateCreatedRun(GameEntity run, GameEntity store,
            GameEntity visit, GameEntity trolley, int productCount)
        {
            if (run == null || run.isDestructed || !run.isWarehouseTask ||
                !run.isWorkerTrolleyCustomerLoadingRun ||
                run.isInboundToStorageTask || run.isStockToCustomerLoadingTask ||
                !run.hasEntityId || !run.hasWarehouseTaskStoreEntityId ||
                run.WarehouseTaskStoreEntityId != store.EntityId ||
                !run.hasWarehouseTaskCustomerVisitEntityId ||
                run.WarehouseTaskCustomerVisitEntityId != visit.EntityId ||
                !run.hasWarehouseTaskWorkerTrolleyEntityId ||
                run.WarehouseTaskWorkerTrolleyEntityId != trolley.EntityId ||
                !run.hasWarehouseRunProductCount ||
                run.WarehouseRunProductCount != productCount ||
                run.hasWarehouseTaskProductEntityId ||
                run.hasWarehouseTaskOrderLineEntityId ||
                run.hasWarehouseTaskReservedLoadingSlotIndex ||
                !run.hasWarehouseTaskStep ||
                run.WarehouseTaskStep != WarehouseTaskStepId.Available ||
                !run.hasWarehouseTaskBlockReason ||
                run.WarehouseTaskBlockReason != WarehouseTaskBlockReasonId.None ||
                !run.hasWarehouseTaskTimeoutRemaining || run.hasAssignedWorkerEntityId)
                throw new InvalidOperationException(
                    "Warehouse task factory returned invalid worker-trolley run.");
        }

        private static void ValidateCreatedDirectTask(GameEntity task,
            GameEntity store, GameEntity visit, BatchCandidate candidate)
        {
            if (task == null || task.isDestructed || !task.isWarehouseTask ||
                !task.isStockToCustomerLoadingTask || task.isInboundToStorageTask ||
                task.isWorkerTrolleyCustomerLoadingRun || !task.hasEntityId ||
                !task.hasWarehouseTaskStoreEntityId ||
                task.WarehouseTaskStoreEntityId != store.EntityId ||
                !task.hasWarehouseTaskProductEntityId ||
                task.WarehouseTaskProductEntityId != candidate.Product.EntityId ||
                !task.hasWarehouseTaskCustomerVisitEntityId ||
                task.WarehouseTaskCustomerVisitEntityId != visit.EntityId ||
                !task.hasWarehouseTaskOrderLineEntityId ||
                task.WarehouseTaskOrderLineEntityId != candidate.Line.EntityId ||
                !task.hasWarehouseTaskReservedLoadingSlotIndex ||
                task.WarehouseTaskReservedLoadingSlotIndex != candidate.LoadingSlotIndex ||
                !task.hasWarehouseTaskStep ||
                task.WarehouseTaskStep != WarehouseTaskStepId.Available ||
                !task.hasWarehouseTaskBlockReason ||
                task.WarehouseTaskBlockReason != WarehouseTaskBlockReasonId.None ||
                !task.hasWarehouseTaskTimeoutRemaining || task.hasAssignedWorkerEntityId)
                throw new InvalidOperationException(
                    "Warehouse task factory returned invalid customer loading task.");
        }

        private static void ValidateStore(GameEntity worker, GameEntity store)
        {
            if (store == null || store.isDestructed || !store.isStore ||
                !store.hasEntityId || !store.hasStorageZoneEntityId)
                throw new InvalidOperationException(
                    $"Warehouse worker {worker.EntityId} has invalid store relation.");
            int phaseCount = (store.isStorePreparing ? 1 : 0) +
                             (store.isStoreOpen ? 1 : 0) +
                             (store.isStoreClosing ? 1 : 0) +
                             (store.isDayReportOpen ? 1 : 0);
            if (phaseCount != 1)
                throw new InvalidOperationException(
                    $"Store {store.EntityId} must have exactly one day phase.");
        }

        private static int CompareOrderLines(GameEntity left, GameEntity right)
        {
            int comparison = left.LineIndex.CompareTo(right.LineIndex);
            return comparison != 0 ? comparison : left.EntityId.CompareTo(right.EntityId);
        }

        private static int CompareShelfProduct(GameEntity left, GameEntity right)
        {
            int comparison = left.StorageSlotIndex.CompareTo(right.StorageSlotIndex);
            return comparison != 0 ? comparison : left.EntityId.CompareTo(right.EntityId);
        }

        private readonly struct BatchCandidate
        {
            public readonly GameEntity Line;
            public readonly GameEntity Product;
            public readonly int LoadingSlotIndex;

            public BatchCandidate(GameEntity line, GameEntity product,
                int loadingSlotIndex)
            {
                Line = line;
                Product = product;
                LoadingSlotIndex = loadingSlotIndex;
            }
        }
    }
}
