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
    public sealed class ExecuteWorkerTrolleyInboundStorageRunSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly WarehouseWorkerConfig _config;
        private readonly IWorkerNavigationService _navigation;
        private readonly IGroup<GameEntity> _runs;
        private readonly IGroup<GameEntity> _stockedProducts;
        private readonly IGroup<GameEntity> _reservedStockProducts;
        private readonly List<GameEntity> _runBuffer = new(2);
        private readonly List<GameEntity> _products = new(3);
        private readonly List<GameEntity> _tasks = new(3);
        private readonly HashSet<int> _reservedStorageSlots = new();
        private readonly bool[] _occupiedCartSlots;

        public ExecuteWorkerTrolleyInboundStorageRunSystem(
            GameContext gameContext, IStaticDataService staticData,
            IWorkerNavigationService navigation)
        {
            _gameContext = gameContext;
            _config = staticData.WarehouseWorker;
            _navigation = navigation;
            _occupiedCartSlots = new bool[staticData.PlatformTrolley.Capacity];
            _runs = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.InboundToStorageTask,
                    GameMatcher.WorkerTrolleyInboundStorageRun,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseTaskStoreEntityId,
                    GameMatcher.WarehouseTaskStorageZoneEntityId,
                    GameMatcher.WarehouseTaskProductEntityId,
                    GameMatcher.WarehouseTaskReservedStorageSlotIndex,
                    GameMatcher.WarehouseTaskWorkerTrolleyEntityId,
                    GameMatcher.WarehouseRunProductCount,
                    GameMatcher.WarehouseTaskStep,
                    GameMatcher.WarehouseTaskBlockReason,
                    GameMatcher.WarehouseTaskTimeoutRemaining)
                .NoneOf(
                    GameMatcher.StockToCustomerLoadingTask,
                    GameMatcher.WorkerTrolleyCustomerLoadingRun,
                    GameMatcher.Destructed));
            _stockedProducts = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Product, GameMatcher.EntityId,
                GameMatcher.InStock, GameMatcher.StorageZoneEntityId,
                GameMatcher.StorageSlotIndex));
            _reservedStockProducts = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Product, GameMatcher.EntityId,
                GameMatcher.InStock, GameMatcher.StorageZoneEntityId,
                GameMatcher.ReservedStorageSlotIndex));
        }

        public void Execute()
        {
            foreach (GameEntity run in _runs.GetEntities(_runBuffer))
            {
                if (!run.hasAssignedWorkerEntityId)
                {
                    if (run.WarehouseTaskStep is WarehouseTaskStepId.Available or
                        WarehouseTaskStepId.Blocked)
                    {
                        continue;
                    }
                    throw new InvalidOperationException(
                        $"Inbound worker-trolley run {run.EntityId} moves without a worker.");
                }
                if (run.WarehouseTaskStep == WarehouseTaskStepId.Blocked)
                    continue;

                GameEntity worker = _gameContext.GetEntityWithEntityId(
                    run.AssignedWorkerEntityId);
                GameEntity trolley = _gameContext.GetEntityWithEntityId(
                    run.WarehouseTaskWorkerTrolleyEntityId);
                if (worker == null || worker.isDestructed ||
                    trolley == null || trolley.isDestructed)
                {
                    continue;
                }
                ValidateWorkerAndTrolley(run, worker, trolley);

                switch (run.WarehouseTaskStep)
                {
                    case WarehouseTaskStepId.MovingToWorkerTrolley:
                        ExecuteMoveToTrolley(run, worker, trolley);
                        break;
                    case WarehouseTaskStepId.MovingWorkerTrolleyToPickup:
                        ExecuteMoveToPickup(run, worker, trolley);
                        break;
                    case WarehouseTaskStepId.MovingWorkerTrolleyToStorage:
                        ExecuteMoveToStorage(run, worker, trolley);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Inbound worker-trolley run {run.EntityId} has invalid step " +
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
            CollectAndValidateBatch(run, trolley, onTrolley: false);
            ValidateStorageReservations(run);
            if (trolley.OccupiedTrolleySlotCount != 0)
                throw new InvalidOperationException(
                    $"Inbound run {run.EntityId} received a non-empty trolley.");

            bool alreadyPushing = trolley.hasTrolleyPusherEntityId;
            if (alreadyPushing)
            {
                if (trolley.TrolleyPusherEntityId != worker.EntityId ||
                    !worker.isPushingWorkerTrolley || !worker.isHandsOccupied)
                {
                    throw new InvalidOperationException(
                        $"Inbound run {run.EntityId} has an invalid retained trolley.");
                }
                MoveToPickup(run, worker);
                ExecuteMoveToPickup(run, worker, trolley);
                return;
            }
            if (worker.isPushingWorkerTrolley || worker.isHandsOccupied)
                throw new InvalidOperationException(
                    $"Worker {worker.EntityId} cannot approach the inbound trolley.");

            Pose homePose = new(trolley.WorkerTrolleyHomePosition,
                trolley.WorkerTrolleyHomeRotation);
            Vector3 pusherTarget =
                WorkerTrolleyLeaseUtility.GetPusherPosition(trolley, homePose);
            if (!HasReached(worker, pusherTarget))
            {
                Navigate(worker, run, pusherTarget,
                    WarehouseTaskBlockReasonId.NoWorkerTrolleyPath);
                return;
            }

            _navigation.Stop(worker.NavigationAgent);
            worker.Transform.rotation = homePose.rotation;
            if (!CartReached(trolley, homePose))
                return;
            trolley.AddTrolleyPusherEntityId(worker.EntityId);
            worker.isPushingWorkerTrolley = true;
            worker.isHandsOccupied = true;
            MoveToPickup(run, worker);
            ExecuteMoveToPickup(run, worker, trolley);
        }

        private void ExecuteMoveToPickup(GameEntity run, GameEntity worker,
            GameEntity trolley)
        {
            if (worker.WarehouseWorkerStatus !=
                WarehouseWorkerStatusId.MovingWorkerTrolleyToPickup)
                throw InvalidStatus(run, worker);
            CollectAndValidateBatch(run, trolley, onTrolley: false);
            ValidateStorageReservations(run);
            ValidatePushedTrolley(run, worker, trolley, expectedCargoCount: 0);

            Pose cartTarget = WorkerTrolleyLeaseUtility.CreateAccessPose(
                worker.WarehouseWorkerPickupPosition,
                worker.WarehouseWorkerPickupRotation,
                trolley.TrolleyFollowDistance);
            Vector3 pusherTarget =
                WorkerTrolleyLeaseUtility.GetPusherPosition(trolley, cartTarget);
            if (!HasReached(worker, pusherTarget))
            {
                Navigate(worker, run, pusherTarget,
                    WarehouseTaskBlockReasonId.NoPickupPath);
                return;
            }

            _navigation.Stop(worker.NavigationAgent);
            worker.Transform.rotation = cartTarget.rotation;
            if (!CartReached(trolley, cartTarget))
                return;
            for (int index = 0; index < _products.Count; index++)
            {
                GameEntity product = _products[index];
                int deliverySlotIndex = product.DeliverySlotIndex;
                product.RemoveDeliverySlotIndex();
                product.AddReservedDeliverySlotIndex(deliverySlotIndex);
                product.AddWorkerTrolleyEntityId(trolley.EntityId);
                product.AddWorkerTrolleySlotIndex(index);
                product.isProductPlacementDirty = true;
            }
            trolley.ReplaceOccupiedTrolleySlotCount(_products.Count);
            run.ReplaceWarehouseTaskStep(
                WarehouseTaskStepId.MovingWorkerTrolleyToStorage);
            run.ReplaceWarehouseTaskTimeoutRemaining(_config.TaskTimeout);
            worker.ReplaceWarehouseWorkerStatus(
                WarehouseWorkerStatusId.MovingWorkerTrolleyToStorage);
        }

        private void ExecuteMoveToStorage(GameEntity run, GameEntity worker,
            GameEntity trolley)
        {
            if (worker.WarehouseWorkerStatus !=
                WarehouseWorkerStatusId.MovingWorkerTrolleyToStorage)
                throw InvalidStatus(run, worker);
            CollectAndValidateBatch(run, trolley, onTrolley: true);
            ValidateStorageReservations(run);
            ValidatePushedTrolley(run, worker, trolley, _products.Count);

            Pose cartTarget = WorkerTrolleyLeaseUtility.CreateStorageAccessPose(
                worker.WarehouseWorkerStoragePosition,
                worker.WarehouseWorkerStorageRotation,
                trolley.TrolleyFollowDistance);
            Vector3 pusherTarget =
                WorkerTrolleyLeaseUtility.GetPusherPosition(trolley, cartTarget);
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
            ValidateStorageReservations(run);
            CompleteStorage(run, worker, trolley);
        }

        private void MoveToPickup(GameEntity run, GameEntity worker)
        {
            _navigation.Stop(worker.NavigationAgent);
            _navigation.SetAutomaticRotation(
                worker.NavigationAgent, enabled: true);
            run.ReplaceWarehouseTaskStep(
                WarehouseTaskStepId.MovingWorkerTrolleyToPickup);
            run.ReplaceWarehouseTaskTimeoutRemaining(_config.TaskTimeout);
            worker.ReplaceWarehouseWorkerStatus(
                WarehouseWorkerStatusId.MovingWorkerTrolleyToPickup);
        }

        private void CompleteStorage(GameEntity run, GameEntity worker,
            GameEntity trolley)
        {
            for (int index = 0; index < _products.Count; index++)
            {
                GameEntity product = _products[index];
                GameEntity task = _tasks[index];
                int slotIndex = task.WarehouseTaskReservedStorageSlotIndex;
                product.RemoveWorkerTrolleyEntityId();
                product.RemoveWorkerTrolleySlotIndex();
                product.RemoveWarehouseRunEntityId();
                product.RemoveReservedDeliverySlotIndex();
                product.isInboundProduct = false;
                product.isInStock = true;
                product.isInteractable = true;
                product.AddStorageZoneEntityId(
                    task.WarehouseTaskStorageZoneEntityId);
                product.AddStorageSlotIndex(slotIndex);
                product.isProductPlacementDirty = true;
                product.isProductStocked = true;
            }

            foreach (GameEntity task in _tasks)
            {
                if (task.hasAssignedWorkerEntityId)
                    task.RemoveAssignedWorkerEntityId();
                if (task.hasWarehouseTaskWorkerTrolleyEntityId)
                    task.RemoveWarehouseTaskWorkerTrolleyEntityId();
                task.RemoveWarehouseTaskReservedStorageSlotIndex();
                task.isDestructed = true;
            }
            trolley.ReplaceOccupiedTrolleySlotCount(0);
            worker.ReplaceWarehouseWorkerStatus(
                WarehouseWorkerStatusId.ReturningWorkerTrolley);
        }

        private void CollectAndValidateBatch(GameEntity run,
            GameEntity trolley, bool onTrolley)
        {
            _products.Clear();
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithWarehouseRunEntityId(run.EntityId))
                _products.Add(product);
            _products.Sort(CompareProductsByStorageSlot);
            if (_products.Count != run.WarehouseRunProductCount ||
                _products.Count < 1 || _products.Count > trolley.TrolleyCapacity)
                throw new InvalidOperationException(
                    $"Inbound worker-trolley run {run.EntityId} has invalid batch size.");
            if (trolley.TrolleyCapacity != _occupiedCartSlots.Length)
                throw new InvalidOperationException(
                    $"Worker trolley {trolley.EntityId} capacity changed at runtime.");

            _tasks.Clear();
            Array.Clear(_occupiedCartSlots, 0, _occupiedCartSlots.Length);
            foreach (GameEntity product in _products)
            {
                InboundProductManifestValidator.Validate(_gameContext, product);
                GameEntity delivery = _gameContext.GetEntityWithEntityId(
                    product.DeliveryEntityId);
                if (delivery.StoreEntityId != run.WarehouseTaskStoreEntityId)
                    throw new InvalidOperationException(
                        $"Inbound run product {product.EntityId} belongs to another store.");
                GameEntity task = GetProductTask(run, product);
                _tasks.Add(task);
                bool cartPlacement = product.hasWorkerTrolleyEntityId &&
                    product.WorkerTrolleyEntityId == trolley.EntityId &&
                    product.hasWorkerTrolleySlotIndex &&
                    product.WorkerTrolleySlotIndex >= 0 &&
                    product.WorkerTrolleySlotIndex < trolley.TrolleyCapacity &&
                    !_occupiedCartSlots[product.WorkerTrolleySlotIndex];
                bool deliveryPlacement = product.hasDeliverySlotIndex &&
                    !product.hasReservedDeliverySlotIndex;
                bool reservedDeliveryPlacement =
                    !product.hasDeliverySlotIndex &&
                    product.hasReservedDeliverySlotIndex;
                if (product.isDestructed || !product.isProduct ||
                    !product.isInboundProduct || product.isInStock ||
                    product.isLoaded || product.isInteractable ||
                    !product.hasEntityId || !product.hasProductType ||
                    !product.hasDeliveryEntityId ||
                    !product.hasPurchaseOrderLineEntityId ||
                    product.hasStorageZoneEntityId ||
                    product.hasStorageSlotIndex ||
                    product.hasReservedStorageSlotIndex ||
                    product.hasCarrierEntityId || product.hasOrderLineEntityId ||
                    product.hasLoadingSlotIndex || product.isLooseProduct ||
                    product.hasTrolleyEntityId || product.hasTrolleySlotIndex ||
                    !product.hasWarehouseRunEntityId ||
                    product.WarehouseRunEntityId != run.EntityId ||
                    onTrolley != cartPlacement ||
                    (onTrolley ? !reservedDeliveryPlacement : !deliveryPlacement))
                {
                    throw new InvalidOperationException(
                        $"Inbound run product {product.EntityId} has an invalid phase.");
                }
                if (!onTrolley &&
                    (product.hasWorkerTrolleyEntityId ||
                     product.hasWorkerTrolleySlotIndex))
                {
                    throw new InvalidOperationException(
                        $"Inbound run product {product.EntityId} has stale trolley placement.");
                }
                if (onTrolley)
                    _occupiedCartSlots[product.WorkerTrolleySlotIndex] = true;
            }
        }

        private GameEntity GetProductTask(GameEntity run, GameEntity product)
        {
            GameEntity task =
                _gameContext.GetEntityWithWarehouseTaskProductEntityId(
                    product.EntityId);
            if (task == null || task.isDestructed || !task.isWarehouseTask ||
                !task.isInboundToStorageTask ||
                task.isStockToCustomerLoadingTask ||
                task.isWorkerTrolleyCustomerLoadingRun ||
                !task.hasEntityId || !task.hasWarehouseTaskStoreEntityId ||
                task.WarehouseTaskStoreEntityId !=
                run.WarehouseTaskStoreEntityId ||
                !task.hasWarehouseTaskStorageZoneEntityId ||
                task.WarehouseTaskStorageZoneEntityId !=
                run.WarehouseTaskStorageZoneEntityId ||
                !task.hasWarehouseTaskReservedStorageSlotIndex ||
                !task.hasWarehouseTaskStep || !task.hasWarehouseTaskBlockReason ||
                !task.hasWarehouseTaskTimeoutRemaining ||
                (task == run) != task.isWorkerTrolleyInboundStorageRun ||
                task != run &&
                (task.hasAssignedWorkerEntityId ||
                 task.WarehouseTaskStep != WarehouseTaskStepId.Available ||
                 task.WarehouseTaskBlockReason != WarehouseTaskBlockReasonId.None ||
                 task.hasWarehouseTaskWorkerTrolleyEntityId ||
                 task.hasWarehouseRunProductCount))
            {
                throw new InvalidOperationException(
                    $"Inbound run product {product.EntityId} has an invalid task.");
            }
            return task;
        }

        private void ValidateStorageReservations(GameEntity run)
        {
            GameEntity storageZone = _gameContext.GetEntityWithEntityId(
                run.WarehouseTaskStorageZoneEntityId);
            if (storageZone == null || storageZone.isDestructed ||
                !storageZone.isStorageZone || !storageZone.hasEntityId ||
                !storageZone.hasSlots)
                throw new InvalidOperationException(
                    $"Inbound run {run.EntityId} has an invalid storage zone.");

            _reservedStorageSlots.Clear();
            foreach (GameEntity task in _tasks)
            {
                int slotIndex = task.WarehouseTaskReservedStorageSlotIndex;
                if (slotIndex < 0 || slotIndex >= storageZone.Slots.Length ||
                    !_reservedStorageSlots.Add(slotIndex))
                    throw new InvalidOperationException(
                        $"Inbound run {run.EntityId} has duplicate storage reservations.");
            }
            foreach (GameEntity stocked in _stockedProducts)
            {
                if (stocked.StorageZoneEntityId == storageZone.EntityId &&
                    _reservedStorageSlots.Contains(stocked.StorageSlotIndex))
                    throw new InvalidOperationException(
                        $"Inbound run {run.EntityId} targets an occupied storage slot.");
            }
            foreach (GameEntity reserved in _reservedStockProducts)
            {
                if (reserved.StorageZoneEntityId == storageZone.EntityId &&
                    _reservedStorageSlots.Contains(
                        reserved.ReservedStorageSlotIndex))
                    throw new InvalidOperationException(
                        $"Inbound run {run.EntityId} targets a reserved stock slot.");
            }
            foreach (GameEntity task in
                     _gameContext.GetEntitiesWithWarehouseTaskStorageZoneEntityId(
                         storageZone.EntityId))
            {
                if (task.isDestructed || !task.isWarehouseTask ||
                    !task.isInboundToStorageTask ||
                    !task.hasWarehouseTaskReservedStorageSlotIndex ||
                    _tasks.Contains(task))
                {
                    continue;
                }
                if (_reservedStorageSlots.Contains(
                        task.WarehouseTaskReservedStorageSlotIndex))
                    throw new InvalidOperationException(
                        $"Inbound run {run.EntityId} duplicates another task reservation.");
            }
        }

        private static void ValidatePushedTrolley(GameEntity run,
            GameEntity worker, GameEntity trolley, int expectedCargoCount)
        {
            if (!trolley.hasTrolleyPusherEntityId ||
                trolley.TrolleyPusherEntityId != worker.EntityId ||
                !worker.isPushingWorkerTrolley || !worker.isHandsOccupied ||
                worker.isCarryingProduct ||
                trolley.OccupiedTrolleySlotCount != expectedCargoCount)
            {
                throw new InvalidOperationException(
                    $"Inbound run {run.EntityId} has an invalid pushed trolley.");
            }
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
                !worker.hasWarehouseWorkerPickupPosition ||
                !worker.hasWarehouseWorkerPickupRotation ||
                !worker.hasWarehouseWorkerStoragePosition ||
                !worker.hasWarehouseWorkerStorageRotation ||
                worker.isCarryingProduct)
                throw new InvalidOperationException(
                    $"Inbound trolley run {run.EntityId} references an invalid worker.");
            if (!trolley.isWorkerTrolley || !trolley.isPlatformTrolley ||
                trolley.isInteractable || !trolley.hasEntityId ||
                trolley.EntityId != run.WarehouseTaskWorkerTrolleyEntityId ||
                !trolley.hasTrolleyStoreEntityId ||
                trolley.TrolleyStoreEntityId != run.WarehouseTaskStoreEntityId ||
                !trolley.hasWorkerTrolleyStoreEntityId ||
                trolley.WorkerTrolleyStoreEntityId !=
                run.WarehouseTaskStoreEntityId ||
                !trolley.hasTrolleyCapacity ||
                !trolley.hasOccupiedTrolleySlotCount ||
                !trolley.hasTrolleyFollowDistance || !trolley.hasTransform ||
                !trolley.hasRigidbody || !trolley.hasColliders ||
                !trolley.hasSlots ||
                trolley.Slots.Length != trolley.TrolleyCapacity ||
                !trolley.hasWorkerTrolleyHomePosition ||
                !trolley.hasWorkerTrolleyHomeRotation ||
                !trolley.hasWorkerTrolleyCustomerLoadingPosition ||
                !trolley.hasWorkerTrolleyCustomerLoadingRotation ||
                _gameContext.GetEntitiesWithTrolleyEntityId(
                    trolley.EntityId).Count != 0)
                throw new InvalidOperationException(
                    $"Inbound trolley run {run.EntityId} references an invalid trolley.");
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

        private bool CartReached(GameEntity trolley, Pose target)
        {
            float tolerance = Mathf.Max(_config.StoppingDistance, 0.1f);
            return (trolley.Transform.position - target.position).sqrMagnitude <=
                   tolerance * tolerance &&
                   Quaternion.Angle(trolley.Transform.rotation, target.rotation) <= 2f;
        }

        private int CompareProductsByStorageSlot(GameEntity left,
            GameEntity right)
        {
            GameEntity leftTask =
                _gameContext.GetEntityWithWarehouseTaskProductEntityId(
                    left.EntityId);
            GameEntity rightTask =
                _gameContext.GetEntityWithWarehouseTaskProductEntityId(
                    right.EntityId);
            if (leftTask == null || rightTask == null ||
                !leftTask.hasWarehouseTaskReservedStorageSlotIndex ||
                !rightTask.hasWarehouseTaskReservedStorageSlotIndex)
                throw new InvalidOperationException(
                    "Inbound trolley batch lost a task storage reservation.");
            int slotComparison =
                leftTask.WarehouseTaskReservedStorageSlotIndex.CompareTo(
                    rightTask.WarehouseTaskReservedStorageSlotIndex);
            return slotComparison != 0
                ? slotComparison
                : left.EntityId.CompareTo(right.EntityId);
        }

        private static InvalidOperationException InvalidStatus(
            GameEntity run, GameEntity worker) => new(
            $"Worker {worker.EntityId} status {worker.WarehouseWorkerStatus} does not " +
            $"match inbound trolley run {run.EntityId} step {run.WarehouseTaskStep}.");
    }
}
