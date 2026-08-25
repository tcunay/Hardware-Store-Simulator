using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Navigation;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class ExecuteWorkerTrolleyRunSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly WarehouseWorkerConfig _config;
        private readonly IWorkerNavigationService _navigation;
        private readonly IGroup<GameEntity> _runs;
        private readonly List<GameEntity> _runBuffer = new(2);
        private readonly List<GameEntity> _products = new(3);
        private readonly bool[] _occupiedCartSlots;
        private readonly bool[] _occupiedLoadingSlots;

        public ExecuteWorkerTrolleyRunSystem(GameContext gameContext,
            IStaticDataService staticData, IWorkerNavigationService navigation)
        {
            _gameContext = gameContext;
            _config = staticData.WarehouseWorker;
            _occupiedCartSlots = new bool[staticData.PlatformTrolley.Capacity];
            _occupiedLoadingSlots = new bool[staticData.CustomerVehicle.CargoCapacity];
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
                    GameMatcher.WarehouseTaskBlockReason,
                    GameMatcher.WarehouseTaskTimeoutRemaining)
                .NoneOf(
                    GameMatcher.InboundToStorageTask,
                    GameMatcher.StockToCustomerLoadingTask,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity run in _runs.GetEntities(_runBuffer))
            {
                if (!run.hasAssignedWorkerEntityId)
                {
                    if (run.WarehouseTaskStep is WarehouseTaskStepId.Available or
                        WarehouseTaskStepId.Blocked)
                        continue;
                    throw new InvalidOperationException(
                        $"Worker-trolley run {run.EntityId} moves without a worker.");
                }
                if (run.WarehouseTaskStep == WarehouseTaskStepId.Blocked)
                    continue;

                GameEntity worker = _gameContext.GetEntityWithEntityId(
                    run.AssignedWorkerEntityId);
                GameEntity trolley = _gameContext.GetEntityWithEntityId(
                    run.WarehouseTaskWorkerTrolleyEntityId);
                if (worker == null || worker.isDestructed ||
                    trolley == null || trolley.isDestructed)
                    continue;
                ValidateWorkerAndTrolley(run, worker, trolley);

                switch (run.WarehouseTaskStep)
                {
                    case WarehouseTaskStepId.MovingToWorkerTrolley:
                        ExecuteMoveToTrolley(run, worker, trolley);
                        break;
                    case WarehouseTaskStepId.MovingWorkerTrolleyToStorage:
                        ExecuteMoveToStorage(run, worker, trolley);
                        break;
                    case WarehouseTaskStepId.MovingWorkerTrolleyToCustomerLoading:
                        ExecuteMoveToCustomer(run, worker, trolley);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Worker-trolley run {run.EntityId} has invalid step " +
                            $"{run.WarehouseTaskStep}.");
                }
            }
        }

        private void ExecuteMoveToTrolley(GameEntity run, GameEntity worker,
            GameEntity trolley)
        {
            if (worker.WarehouseWorkerStatus !=
                WarehouseWorkerStatusId.MovingToWorkerTrolley)
                throw InvalidStatus(run, worker);
            CollectAndValidateProducts(run, trolley, onTrolley: false);
            if (trolley.OccupiedTrolleySlotCount != 0)
                throw new InvalidOperationException(
                    $"Worker trolley {trolley.EntityId} is not empty before loading.");

            Pose cartTarget = HomePose(trolley);
            Vector3 pusherTarget = WorkerTrolleyLeaseUtility.GetPusherPosition(
                trolley, cartTarget);
            bool alreadyPushing = trolley.hasTrolleyPusherEntityId;
            if (alreadyPushing &&
                (trolley.TrolleyPusherEntityId != worker.EntityId ||
                 !worker.isPushingWorkerTrolley || !worker.isHandsOccupied))
                throw new InvalidOperationException(
                    $"Worker-trolley run {run.EntityId} has invalid retained pusher.");
            if (!alreadyPushing &&
                (worker.isPushingWorkerTrolley || worker.isHandsOccupied))
                throw new InvalidOperationException(
                    $"Worker {worker.EntityId} cannot approach trolley while occupied.");
            if (alreadyPushing)
            {
                MoveToStorage(run, worker);
                ExecuteMoveToStorage(run, worker, trolley);
                return;
            }

            if (!HasReached(worker, pusherTarget))
            {
                Navigate(worker, run, pusherTarget,
                    WarehouseTaskBlockReasonId.NoWorkerTrolleyPath);
                return;
            }

            _navigation.Stop(worker.NavigationAgent);
            worker.Transform.rotation = cartTarget.rotation;
            if (!CartReached(trolley, cartTarget))
                return;
            trolley.AddTrolleyPusherEntityId(worker.EntityId);
            worker.isPushingWorkerTrolley = true;
            worker.isHandsOccupied = true;
            MoveToStorage(run, worker);
            ExecuteMoveToStorage(run, worker, trolley);
        }

        private void ExecuteMoveToStorage(GameEntity run, GameEntity worker,
            GameEntity trolley)
        {
            if (worker.WarehouseWorkerStatus !=
                WarehouseWorkerStatusId.MovingWorkerTrolleyToStorage)
                throw InvalidStatus(run, worker);
            CollectAndValidateProducts(run, trolley, onTrolley: false);
            if (!trolley.hasTrolleyPusherEntityId ||
                trolley.TrolleyPusherEntityId != worker.EntityId ||
                !worker.isPushingWorkerTrolley || !worker.isHandsOccupied ||
                trolley.OccupiedTrolleySlotCount != 0)
            {
                throw new InvalidOperationException(
                    $"Worker-trolley run {run.EntityId} has invalid empty trolley state.");
            }

            Pose cartTarget = StoragePose(worker, trolley);
            Vector3 pusherTarget = WorkerTrolleyLeaseUtility.GetPusherPosition(
                trolley, cartTarget);
            if (!HasReached(worker, pusherTarget))
            {
                Navigate(worker, run, pusherTarget,
                    WarehouseTaskBlockReasonId.NoStoragePath);
                return;
            }

            _navigation.Stop(worker.NavigationAgent);
            worker.Transform.rotation = cartTarget.rotation;
            if (!CartReached(trolley, cartTarget))
                return;
            for (int index = 0; index < _products.Count; index++)
            {
                GameEntity product = _products[index];
                product.AddWorkerTrolleyEntityId(trolley.EntityId);
                product.AddWorkerTrolleySlotIndex(index);
                product.isProductPlacementDirty = true;
            }
            trolley.ReplaceOccupiedTrolleySlotCount(_products.Count);
            run.ReplaceWarehouseTaskStep(
                WarehouseTaskStepId.MovingWorkerTrolleyToCustomerLoading);
            run.ReplaceWarehouseTaskTimeoutRemaining(_config.TaskTimeout);
            worker.ReplaceWarehouseWorkerStatus(
                WarehouseWorkerStatusId.MovingWorkerTrolleyToCustomerLoading);
        }

        private void MoveToStorage(GameEntity run, GameEntity worker)
        {
            _navigation.Stop(worker.NavigationAgent);
            _navigation.SetAutomaticRotation(
                worker.NavigationAgent, enabled: true);
            run.ReplaceWarehouseTaskStep(
                WarehouseTaskStepId.MovingWorkerTrolleyToStorage);
            run.ReplaceWarehouseTaskTimeoutRemaining(_config.TaskTimeout);
            worker.ReplaceWarehouseWorkerStatus(
                WarehouseWorkerStatusId.MovingWorkerTrolleyToStorage);
        }

        private void ExecuteMoveToCustomer(GameEntity run, GameEntity worker,
            GameEntity trolley)
        {
            if (worker.WarehouseWorkerStatus !=
                WarehouseWorkerStatusId.MovingWorkerTrolleyToCustomerLoading)
                throw InvalidStatus(run, worker);
            CollectAndValidateProducts(run, trolley, onTrolley: true);
            if (!trolley.hasTrolleyPusherEntityId ||
                trolley.TrolleyPusherEntityId != worker.EntityId ||
                !worker.isPushingWorkerTrolley || !worker.isHandsOccupied ||
                trolley.OccupiedTrolleySlotCount != _products.Count)
                throw new InvalidOperationException(
                    $"Worker-trolley run {run.EntityId} has invalid loaded trolley state.");

            GameEntity visit = GetLoadingVisit(run);
            ValidateAtomicUnload(run, visit);
            Pose cartTarget = CustomerLoadingPose(trolley);
            Vector3 pusherTarget = WorkerTrolleyLeaseUtility.GetPusherPosition(
                trolley, cartTarget);
            if (!HasReached(worker, pusherTarget))
            {
                Navigate(worker, run, pusherTarget,
                    WarehouseTaskBlockReasonId.NoCustomerLoadingPath);
                return;
            }

            _navigation.Stop(worker.NavigationAgent);
            worker.Transform.rotation = cartTarget.rotation;
            if (!CartReached(trolley, cartTarget))
                return;
            foreach (GameEntity product in _products)
                UnloadProduct(run, visit, product);
            trolley.ReplaceOccupiedTrolleySlotCount(0);
            run.RemoveAssignedWorkerEntityId();
            run.isDestructed = true;
            worker.ReplaceWarehouseWorkerStatus(
                WarehouseWorkerStatusId.ReturningWorkerTrolley);
        }

        private void CollectAndValidateProducts(GameEntity run,
            GameEntity trolley, bool onTrolley)
        {
            _products.Clear();
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithWarehouseRunEntityId(run.EntityId))
                _products.Add(product);
            _products.Sort(CompareRunProducts);
            if (_products.Count != run.WarehouseRunProductCount ||
                _products.Count < 1 || _products.Count > trolley.TrolleyCapacity)
                throw new InvalidOperationException(
                    $"Worker-trolley run {run.EntityId} has invalid product count.");

            if (trolley.TrolleyCapacity != _occupiedCartSlots.Length)
                throw new InvalidOperationException(
                    $"Worker trolley {trolley.EntityId} capacity changed at runtime.");
            Array.Clear(_occupiedCartSlots, 0, _occupiedCartSlots.Length);
            foreach (GameEntity product in _products)
            {
                if (product == null)
                    throw new InvalidOperationException(
                        $"Worker-trolley run {run.EntityId} contains a missing product.");
                bool trolleyState = product.hasWorkerTrolleyEntityId &&
                    product.WorkerTrolleyEntityId == trolley.EntityId &&
                    product.hasWorkerTrolleySlotIndex &&
                    product.WorkerTrolleySlotIndex >= 0 &&
                    product.WorkerTrolleySlotIndex < trolley.TrolleyCapacity &&
                    !_occupiedCartSlots[product.WorkerTrolleySlotIndex];
                bool placementValid = onTrolley
                    ? trolleyState
                    : !product.hasWorkerTrolleyEntityId &&
                      !product.hasWorkerTrolleySlotIndex;
                if (product.isDestructed || !product.isProduct || !product.isInStock ||
                    product.isInboundProduct || product.isLoaded ||
                    product.isInteractable || !product.hasEntityId ||
                    !product.hasProductType ||
                    !product.hasStorageZoneEntityId ||
                    !product.hasReservedStorageSlotIndex ||
                    !product.hasReservedOrderLineEntityId ||
                    !product.hasReservedCustomerLoadingSlotIndex ||
                    !product.hasWarehouseRunEntityId ||
                    product.WarehouseRunEntityId != run.EntityId ||
                    product.hasStorageSlotIndex || product.hasCarrierEntityId ||
                    product.hasOrderLineEntityId || product.hasLoadingSlotIndex ||
                    product.hasTrolleyEntityId || product.hasTrolleySlotIndex ||
                    !placementValid)
                    throw new InvalidOperationException(
                        $"Worker-trolley run {run.EntityId} has invalid reserved product.");
                if (onTrolley)
                    _occupiedCartSlots[product.WorkerTrolleySlotIndex] = true;
            }
        }

        private GameEntity GetLoadingVisit(GameEntity run)
        {
            GameEntity visit = _gameContext.GetEntityWithEntityId(
                run.WarehouseTaskCustomerVisitEntityId);
            if (visit == null || visit.isDestructed || !visit.isCustomerVisit ||
                !visit.isCustomerVehicle || !visit.isCustomerVisitLoading ||
                !visit.isOrder || !visit.hasEntityId ||
                !visit.hasCustomerVisitStoreEntityId ||
                visit.CustomerVisitStoreEntityId != run.WarehouseTaskStoreEntityId ||
                !visit.hasStorageZoneEntityId || !visit.hasSlots ||
                !visit.hasReservedCustomerLoadingBayEntityId)
                throw new InvalidOperationException(
                    $"Worker-trolley run {run.EntityId} references inactive visit.");
            GameEntity bay = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerLoadingBayEntityId);
            if (bay == null || bay.isDestructed ||
                !bay.isCustomerLoadingBay || !bay.hasEntityId ||
                !bay.hasCustomerLoadingBayStoreEntityId ||
                bay.CustomerLoadingBayStoreEntityId !=
                run.WarehouseTaskStoreEntityId ||
                _gameContext.GetEntityWithReservedCustomerLoadingBayEntityId(
                    bay.EntityId) != visit)
                throw new InvalidOperationException(
                    $"Worker-trolley run {run.EntityId} lost loading-bay ownership.");
            return visit;
        }

        private void ValidateAtomicUnload(GameEntity run, GameEntity visit)
        {
            if (visit.Slots.Length != _occupiedLoadingSlots.Length)
                throw new InvalidOperationException(
                    $"Visit {visit.EntityId} cargo capacity changed at runtime.");
            Array.Clear(_occupiedLoadingSlots, 0, _occupiedLoadingSlots.Length);
            foreach (GameEntity product in _products)
                ValidateRunProductTarget(run, visit, product);

            foreach (GameEntity line in
                     _gameContext.GetEntitiesWithOrderEntityId(visit.EntityId))
            {
                ValidateOrderLine(visit, line);
                int linkedCount = 0;
                foreach (GameEntity loaded in
                         _gameContext.GetEntitiesWithOrderLineEntityId(line.EntityId))
                {
                    if (loaded.isDestructed || !loaded.isProduct ||
                        !loaded.isLoaded || !loaded.hasLoadingSlotIndex)
                        throw new InvalidOperationException(
                            $"Order line {line.EntityId} has invalid loaded cargo.");
                    ReserveLoadingSlot(visit, loaded.LoadingSlotIndex,
                        loaded.EntityId);
                    linkedCount++;
                }

                int reservedCount = 0;
                foreach (GameEntity reserved in
                         _gameContext.GetEntitiesWithReservedOrderLineEntityId(
                             line.EntityId))
                {
                    if (reserved.isDestructed || !reserved.isProduct ||
                        !reserved.isInStock ||
                        !reserved.hasProductType ||
                        reserved.ProductType != line.ProductType ||
                        !reserved.hasStorageZoneEntityId ||
                        reserved.StorageZoneEntityId !=
                        visit.StorageZoneEntityId ||
                        !reserved.hasReservedStorageSlotIndex ||
                        !reserved.hasReservedOrderLineEntityId ||
                        reserved.ReservedOrderLineEntityId != line.EntityId)
                        throw new InvalidOperationException(
                            $"Order line {line.EntityId} has invalid reservation.");
                    if (reserved.hasReservedCustomerLoadingSlotIndex)
                        ReserveLoadingSlot(visit,
                            reserved.ReservedCustomerLoadingSlotIndex,
                            reserved.EntityId);
                    reservedCount++;
                }
                if (linkedCount < line.LoadedProductCount ||
                    linkedCount + reservedCount > line.RequiredProductCount)
                    throw new InvalidOperationException(
                        $"Order line {line.EntityId} cannot accept trolley-run cargo.");
            }

            foreach (GameEntity task in
                     _gameContext.GetEntitiesWithWarehouseTaskCustomerVisitEntityId(
                         visit.EntityId))
            {
                if (task == run || task.isDestructed ||
                    !task.hasWarehouseTaskReservedLoadingSlotIndex)
                    continue;
                if (!task.isWarehouseTask ||
                    !task.isStockToCustomerLoadingTask)
                    throw new InvalidOperationException(
                        $"Visit {visit.EntityId} has invalid direct-task reservation.");
                ReserveLoadingSlot(visit,
                    task.WarehouseTaskReservedLoadingSlotIndex, task.EntityId);
            }
        }

        private void ValidateRunProductTarget(GameEntity run,
            GameEntity visit, GameEntity product)
        {
            GameEntity line = _gameContext.GetEntityWithEntityId(
                product.ReservedOrderLineEntityId);
            if (line == null || line.isDestructed || !line.isOrderLine ||
                !line.hasEntityId || !line.hasOrderEntityId ||
                line.OrderEntityId != visit.EntityId ||
                !line.hasStorageZoneEntityId ||
                line.StorageZoneEntityId != visit.StorageZoneEntityId ||
                !line.hasProductType || line.ProductType != product.ProductType ||
                !line.hasRequiredProductCount || !line.hasLoadedProductCount ||
                !product.hasWarehouseRunEntityId ||
                product.WarehouseRunEntityId != run.EntityId)
                throw new InvalidOperationException(
                    $"Run product {product.EntityId} has an invalid unload target.");
        }

        private static void ValidateOrderLine(GameEntity visit,
            GameEntity line)
        {
            if (line.isDestructed || !line.isOrderLine || !line.hasEntityId ||
                !line.hasOrderEntityId || line.OrderEntityId != visit.EntityId ||
                !line.hasProductType || !line.hasRequiredProductCount ||
                !line.hasLoadedProductCount || line.RequiredProductCount <= 0 ||
                line.LoadedProductCount < 0 ||
                line.LoadedProductCount > line.RequiredProductCount)
                throw new InvalidOperationException(
                    $"Visit {visit.EntityId} has an invalid order line.");
        }

        private void ReserveLoadingSlot(GameEntity visit, int slotIndex,
            int ownerEntityId)
        {
            if (slotIndex < 0 || slotIndex >= _occupiedLoadingSlots.Length ||
                _occupiedLoadingSlots[slotIndex])
                throw new InvalidOperationException(
                    $"Entity {ownerEntityId} duplicates an occupied loading slot " +
                    $"for visit {visit.EntityId}.");
            _occupiedLoadingSlots[slotIndex] = true;
        }

        private void UnloadProduct(GameEntity run, GameEntity visit,
            GameEntity product)
        {
            GameEntity line = _gameContext.GetEntityWithEntityId(
                product.ReservedOrderLineEntityId);
            if (line == null || line.isDestructed || !line.isOrderLine ||
                !line.hasEntityId || !line.hasOrderEntityId ||
                line.OrderEntityId != visit.EntityId || !line.hasProductType ||
                line.ProductType != product.ProductType ||
                !line.hasRequiredProductCount || !line.hasLoadedProductCount)
                throw new InvalidOperationException(
                    $"Run product {product.EntityId} has invalid order line.");
            int loadingSlotIndex = product.ReservedCustomerLoadingSlotIndex;
            if (loadingSlotIndex < 0 || loadingSlotIndex >= visit.Slots.Length)
                throw new InvalidOperationException(
                    $"Run product {product.EntityId} has invalid loading slot.");

            product.RemoveWorkerTrolleyEntityId();
            product.RemoveWorkerTrolleySlotIndex();
            product.RemoveWarehouseRunEntityId();
            product.RemoveReservedStorageSlotIndex();
            product.RemoveReservedOrderLineEntityId();
            product.RemoveReservedCustomerLoadingSlotIndex();
            product.RemoveStorageZoneEntityId();
            product.isInStock = false;
            product.isLoaded = true;
            product.isInteractable = false;
            product.AddOrderLineEntityId(line.EntityId);
            product.AddLoadingSlotIndex(loadingSlotIndex);
            product.isProductLoaded = true;
            product.isProductPlacementDirty = true;
        }

        private void ValidateWorkerAndTrolley(GameEntity run,
            GameEntity worker, GameEntity trolley)
        {
            if (!worker.isWarehouseWorker || !worker.hasEntityId ||
                worker.EntityId != run.AssignedWorkerEntityId ||
                !worker.hasWarehouseWorkerStoreEntityId ||
                worker.WarehouseWorkerStoreEntityId !=
                run.WarehouseTaskStoreEntityId ||
                !worker.hasWarehouseWorkerStatus || !worker.hasTransform ||
                !worker.hasNavigationAgent ||
                !worker.hasWarehouseWorkerStoragePosition ||
                !worker.hasWarehouseWorkerStorageRotation ||
                worker.isCarryingProduct)
                throw new InvalidOperationException(
                    $"Worker-trolley run {run.EntityId} references invalid worker.");
            if (!trolley.isWorkerTrolley || !trolley.isPlatformTrolley ||
                trolley.isInteractable || !trolley.hasEntityId ||
                trolley.EntityId != run.WarehouseTaskWorkerTrolleyEntityId ||
                !trolley.hasTrolleyStoreEntityId ||
                trolley.TrolleyStoreEntityId != run.WarehouseTaskStoreEntityId ||
                !trolley.hasWorkerTrolleyStoreEntityId ||
                trolley.WorkerTrolleyStoreEntityId != run.WarehouseTaskStoreEntityId ||
                !trolley.hasTrolleyCapacity || !trolley.hasOccupiedTrolleySlotCount ||
                !trolley.hasTrolleyFollowDistance || !trolley.hasTransform ||
                !trolley.hasRigidbody || !trolley.hasColliders ||
                !trolley.hasSlots || trolley.Slots.Length != trolley.TrolleyCapacity ||
                !trolley.hasWorkerTrolleyHomePosition ||
                !trolley.hasWorkerTrolleyHomeRotation ||
                !trolley.hasWorkerTrolleyCustomerLoadingPosition ||
                !trolley.hasWorkerTrolleyCustomerLoadingRotation ||
                _gameContext.GetEntitiesWithTrolleyEntityId(
                    trolley.EntityId).Count != 0)
                throw new InvalidOperationException(
                    $"Worker-trolley run {run.EntityId} references invalid trolley.");
        }

        private bool HasReached(GameEntity worker, Vector3 destination)
        {
            float tolerance = Mathf.Max(_config.StoppingDistance, 0.05f);
            return _navigation.HasReachedDestination(worker.NavigationAgent,
                worker.Transform.position, destination, tolerance);
        }

        private void Navigate(GameEntity worker, GameEntity run,
            Vector3 destination, WarehouseTaskBlockReasonId blockReason)
        {
            if (_navigation.GetState(worker.NavigationAgent) ==
                WorkerNavigationStateId.Moving)
                return;
            if (!_navigation.TrySetDestination(worker.NavigationAgent,
                    destination, _config.NavigationSampleRadius))
            {
                run.ReplaceWarehouseTaskStep(WarehouseTaskStepId.Blocked);
                run.ReplaceWarehouseTaskBlockReason(blockReason);
            }
        }

        private static Pose HomePose(GameEntity trolley) => new(
            trolley.WorkerTrolleyHomePosition,
            trolley.WorkerTrolleyHomeRotation);

        private static Pose CustomerLoadingPose(GameEntity trolley) => new(
            trolley.WorkerTrolleyCustomerLoadingPosition,
            trolley.WorkerTrolleyCustomerLoadingRotation);

        private static Pose StoragePose(GameEntity worker,
            GameEntity trolley) =>
            WorkerTrolleyLeaseUtility.CreateStorageAccessPose(
            worker.WarehouseWorkerStoragePosition,
            worker.WarehouseWorkerStorageRotation,
            trolley.TrolleyFollowDistance);

        private bool CartReached(GameEntity trolley, Pose target)
        {
            float tolerance = Mathf.Max(_config.StoppingDistance, 0.1f);
            return (trolley.Transform.position - target.position).sqrMagnitude <=
                   tolerance * tolerance &&
                   Quaternion.Angle(trolley.Transform.rotation, target.rotation) <= 2f;
        }

        private static int CompareRunProducts(GameEntity left, GameEntity right)
        {
            int slotComparison = left.ReservedCustomerLoadingSlotIndex.CompareTo(
                right.ReservedCustomerLoadingSlotIndex);
            return slotComparison != 0
                ? slotComparison
                : left.EntityId.CompareTo(right.EntityId);
        }

        private static InvalidOperationException InvalidStatus(GameEntity run,
            GameEntity worker) => new(
            $"Worker {worker.EntityId} status {worker.WarehouseWorkerStatus} does not " +
            $"match worker-trolley run {run.EntityId} step {run.WarehouseTaskStep}.");
    }
}
