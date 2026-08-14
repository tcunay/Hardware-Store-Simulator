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
    public sealed class ExecuteCustomerLoadingTaskSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly WarehouseWorkerConfig _config;
        private readonly IWorkerNavigationService _navigation;
        private readonly IGroup<GameEntity> _tasks;
        private readonly List<GameEntity> _buffer = new(4);

        public ExecuteCustomerLoadingTaskSystem(GameContext gameContext,
            IStaticDataService staticData, IWorkerNavigationService navigation)
        {
            _gameContext = gameContext;
            _config = staticData.WarehouseWorker;
            _navigation = navigation;
            _tasks = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.StockToCustomerLoadingTask,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseTaskStoreEntityId,
                    GameMatcher.WarehouseTaskProductEntityId,
                    GameMatcher.WarehouseTaskCustomerVisitEntityId,
                    GameMatcher.WarehouseTaskOrderLineEntityId,
                    GameMatcher.WarehouseTaskReservedLoadingSlotIndex,
                    GameMatcher.WarehouseTaskStep,
                    GameMatcher.WarehouseTaskBlockReason,
                    GameMatcher.WarehouseTaskTimeoutRemaining)
                .NoneOf(
                    GameMatcher.InboundToStorageTask,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity task in _tasks.GetEntities(_buffer))
            {
                if (!task.hasAssignedWorkerEntityId)
                {
                    if (task.WarehouseTaskStep is WarehouseTaskStepId.Available or
                        WarehouseTaskStepId.Blocked)
                    {
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"Customer loading task {task.EntityId} is moving without a worker.");
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
                    case WarehouseTaskStepId.MovingToCustomerLoading:
                        ExecuteLoadingStep(worker, task);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Customer loading task {task.EntityId} has invalid step " +
                            $"{task.WarehouseTaskStep}.");
                }
            }
        }

        private void ExecutePickupStep(GameEntity worker, GameEntity task)
        {
            if (worker.WarehouseWorkerStatus != WarehouseWorkerStatusId.MovingToPickup)
                throw InvalidWorkerStatus(worker, task);
            GameEntity visit = GetLoadingVisit(task);
            GameEntity line = GetOrderLine(task, visit);
            GameEntity product = GetReservedProduct(task, visit, line, false);
            ValidateLoadingSlotReservation(task, visit);

            if (!HasReached(worker, worker.WarehouseWorkerStoragePosition))
            {
                Navigate(worker, task, worker.WarehouseWorkerStoragePosition,
                    WarehouseTaskBlockReasonId.NoPickupPath);
                return;
            }

            _navigation.Stop(worker.NavigationAgent);
            worker.Transform.rotation = worker.WarehouseWorkerStorageRotation;
            product.AddCarrierEntityId(worker.EntityId);
            product.isProductPlacementDirty = true;
            worker.isHandsOccupied = true;
            worker.isCarryingProduct = true;
            task.ReplaceWarehouseTaskStep(
                WarehouseTaskStepId.MovingToCustomerLoading);
            task.ReplaceWarehouseTaskTimeoutRemaining(_config.TaskTimeout);
            worker.ReplaceWarehouseWorkerStatus(
                WarehouseWorkerStatusId.MovingToCustomerLoading);
        }

        private void ExecuteLoadingStep(GameEntity worker, GameEntity task)
        {
            if (worker.WarehouseWorkerStatus !=
                WarehouseWorkerStatusId.MovingToCustomerLoading)
            {
                throw InvalidWorkerStatus(worker, task);
            }
            GameEntity visit = GetLoadingVisit(task);
            GameEntity line = GetOrderLine(task, visit);
            GameEntity product = GetReservedProduct(task, visit, line, true);
            if (product.CarrierEntityId != worker.EntityId ||
                !worker.isHandsOccupied || !worker.isCarryingProduct)
            {
                throw new InvalidOperationException(
                    $"Customer loading task {task.EntityId} has invalid carrier state.");
            }
            ValidateLoadingSlotReservation(task, visit);

            if (!HasReached(worker,
                    worker.WarehouseWorkerCustomerLoadingPosition))
            {
                Navigate(worker, task,
                    worker.WarehouseWorkerCustomerLoadingPosition,
                    WarehouseTaskBlockReasonId.NoCustomerLoadingPath);
                return;
            }

            CompleteLoading(worker, task, visit, line, product);
        }

        private void CompleteLoading(GameEntity worker, GameEntity task,
            GameEntity visit, GameEntity line, GameEntity product)
        {
            int linkedProductCount = CountLinkedProducts(line);
            int reservedProductCount = CountReservedProducts(line);
            if (linkedProductCount < line.LoadedProductCount ||
                linkedProductCount + reservedProductCount >
                line.RequiredProductCount || linkedProductCount >=
                line.RequiredProductCount)
            {
                throw new InvalidOperationException(
                    $"Order line {line.EntityId} cannot accept task product " +
                    $"{product.EntityId}.");
            }

            int slotIndex = task.WarehouseTaskReservedLoadingSlotIndex;
            ValidateLoadingSlotReservation(task, visit);
            _navigation.Stop(worker.NavigationAgent);
            worker.Transform.rotation =
                worker.WarehouseWorkerCustomerLoadingRotation;
            worker.isHandsOccupied = false;
            worker.isCarryingProduct = false;

            product.RemoveCarrierEntityId();
            product.isInStock = false;
            product.isLoaded = true;
            product.isInteractable = false;
            product.RemoveStorageZoneEntityId();
            product.RemoveReservedStorageSlotIndex();
            product.RemoveReservedOrderLineEntityId();
            product.AddOrderLineEntityId(line.EntityId);
            product.AddLoadingSlotIndex(slotIndex);
            product.isProductLoaded = true;
            product.isProductPlacementDirty = true;

            task.RemoveAssignedWorkerEntityId();
            task.RemoveWarehouseTaskReservedLoadingSlotIndex();
            task.isDestructed = true;
            worker.ReplaceWarehouseWorkerStatus(worker.isWorkerShiftActive
                ? WarehouseWorkerStatusId.Idle
                : WarehouseWorkerStatusId.OffShift);
        }

        private GameEntity GetLoadingVisit(GameEntity task)
        {
            GameEntity visit = _gameContext.GetEntityWithEntityId(
                task.WarehouseTaskCustomerVisitEntityId);
            if (visit == null || visit.isDestructed ||
                !visit.isCustomerVisit || !visit.isCustomerVehicle ||
                !visit.isCustomerVisitLoading || !visit.isOrder ||
                !visit.hasEntityId || !visit.hasCustomerVisitStoreEntityId ||
                visit.CustomerVisitStoreEntityId !=
                task.WarehouseTaskStoreEntityId ||
                !visit.hasStorageZoneEntityId || !visit.hasSlots ||
                !visit.hasReservedCustomerLoadingBayEntityId)
            {
                throw new InvalidOperationException(
                    $"Customer loading task {task.EntityId} references inactive visit " +
                    $"{task.WarehouseTaskCustomerVisitEntityId}.");
            }

            GameEntity bay = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerLoadingBayEntityId);
            if (bay == null || bay.isDestructed ||
                !bay.isCustomerLoadingBay || !bay.hasEntityId ||
                !bay.hasCustomerLoadingBayStoreEntityId ||
                bay.CustomerLoadingBayStoreEntityId !=
                visit.CustomerVisitStoreEntityId ||
                _gameContext.GetEntityWithReservedCustomerLoadingBayEntityId(
                    bay.EntityId) != visit)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has invalid loading-bay ownership.");
            }
            return visit;
        }

        private GameEntity GetOrderLine(GameEntity task, GameEntity visit)
        {
            GameEntity line = _gameContext.GetEntityWithEntityId(
                task.WarehouseTaskOrderLineEntityId);
            if (line == null || line.isDestructed || !line.isOrderLine ||
                !line.hasEntityId || !line.hasOrderEntityId ||
                line.OrderEntityId != visit.EntityId ||
                !line.hasStorageZoneEntityId ||
                line.StorageZoneEntityId != visit.StorageZoneEntityId ||
                !line.hasProductType || !line.hasRequiredProductCount ||
                !line.hasLoadedProductCount || line.RequiredProductCount <= 0 ||
                line.LoadedProductCount < 0 ||
                line.LoadedProductCount > line.RequiredProductCount)
            {
                throw new InvalidOperationException(
                    $"Customer loading task {task.EntityId} references invalid order line " +
                    $"{task.WarehouseTaskOrderLineEntityId}.");
            }
            return line;
        }

        private GameEntity GetReservedProduct(GameEntity task, GameEntity visit,
            GameEntity line, bool requireCarrier)
        {
            GameEntity product = _gameContext.GetEntityWithEntityId(
                task.WarehouseTaskProductEntityId);
            bool carrierStateValid = requireCarrier
                ? product != null && product.hasCarrierEntityId
                : product != null && !product.hasCarrierEntityId;
            if (product == null || product.isDestructed || !product.isProduct ||
                !product.hasEntityId || !product.hasProductType ||
                product.ProductType != line.ProductType ||
                !product.isInStock || product.isInboundProduct || product.isLoaded ||
                product.isInteractable || !product.hasStorageZoneEntityId ||
                product.StorageZoneEntityId != visit.StorageZoneEntityId ||
                !product.hasReservedStorageSlotIndex ||
                !product.hasReservedOrderLineEntityId ||
                product.ReservedOrderLineEntityId != line.EntityId ||
                product.hasStorageSlotIndex || product.hasOrderLineEntityId ||
                product.hasLoadingSlotIndex || product.hasDeliveryEntityId ||
                product.hasDeliverySlotIndex ||
                product.hasReservedDeliverySlotIndex || product.isLooseProduct ||
                product.hasTrolleyEntityId || product.hasTrolleySlotIndex ||
                product.hasWorldPosition || product.hasWorldRotation ||
                !carrierStateValid)
            {
                throw new InvalidOperationException(
                    $"Customer loading task {task.EntityId} has invalid reserved product.");
            }

            GameEntity storageZone = _gameContext.GetEntityWithEntityId(
                product.StorageZoneEntityId);
            if (storageZone == null || storageZone.isDestructed ||
                !storageZone.isStorageZone || !storageZone.hasSlots ||
                product.ReservedStorageSlotIndex < 0 ||
                product.ReservedStorageSlotIndex >= storageZone.Slots.Length)
            {
                throw new InvalidOperationException(
                    $"Task product {product.EntityId} has invalid storage reservation.");
            }
            return product;
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

        private void ValidateLoadingSlotReservation(GameEntity ownerTask,
            GameEntity visit)
        {
            int slotIndex = ownerTask.WarehouseTaskReservedLoadingSlotIndex;
            if (slotIndex < 0 || slotIndex >= visit.Slots.Length)
            {
                throw new InvalidOperationException(
                    $"Task {ownerTask.EntityId} reserves invalid customer loading slot " +
                    $"{slotIndex}.");
            }

            foreach (GameEntity line in
                     _gameContext.GetEntitiesWithOrderEntityId(visit.EntityId))
            {
                if (!line.isOrderLine || line.isDestructed || !line.hasEntityId)
                {
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has an invalid order line.");
                }
                foreach (GameEntity product in
                         _gameContext.GetEntitiesWithOrderLineEntityId(line.EntityId))
                {
                    if (!product.isDestructed && product.isProduct &&
                        product.isLoaded && product.hasLoadingSlotIndex &&
                        product.LoadingSlotIndex == slotIndex)
                    {
                        throw new InvalidOperationException(
                            $"Task {ownerTask.EntityId} loading slot is already occupied.");
                    }
                }
            }

            foreach (GameEntity task in
                     _gameContext.GetEntitiesWithWarehouseTaskCustomerVisitEntityId(
                         visit.EntityId))
            {
                if (task == ownerTask || task.isDestructed ||
                    !task.isStockToCustomerLoadingTask ||
                    !task.hasWarehouseTaskReservedLoadingSlotIndex)
                {
                    continue;
                }
                if (task.WarehouseTaskReservedLoadingSlotIndex == slotIndex)
                {
                    throw new InvalidOperationException(
                        $"Task {ownerTask.EntityId} loading slot is reserved twice.");
                }
            }
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
                task.ReplaceWarehouseTaskStep(WarehouseTaskStepId.Blocked);
                task.ReplaceWarehouseTaskBlockReason(blockReason);
            }
        }

        private static void ValidateWorker(GameEntity task, GameEntity worker)
        {
            if (!worker.isWarehouseWorker || !worker.hasEntityId ||
                worker.EntityId != task.AssignedWorkerEntityId ||
                !worker.hasWarehouseWorkerStoreEntityId ||
                worker.WarehouseWorkerStoreEntityId !=
                task.WarehouseTaskStoreEntityId ||
                !worker.hasWarehouseWorkerStatus || !worker.hasTransform ||
                !worker.hasNavigationAgent || !worker.hasCarryAnchor ||
                !worker.hasWarehouseWorkerStoragePosition ||
                !worker.hasWarehouseWorkerStorageRotation ||
                !worker.hasWarehouseWorkerCustomerLoadingPosition ||
                !worker.hasWarehouseWorkerCustomerLoadingRotation)
            {
                throw new InvalidOperationException(
                    $"Customer loading task {task.EntityId} references invalid worker.");
            }
        }

        private static InvalidOperationException InvalidWorkerStatus(
            GameEntity worker, GameEntity task) =>
            new($"Worker {worker.EntityId} status {worker.WarehouseWorkerStatus} does not " +
                $"match customer loading task {task.EntityId} step " +
                $"{task.WarehouseTaskStep}.");
    }
}
