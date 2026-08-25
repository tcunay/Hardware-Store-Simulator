using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class ValidateWarehouseWorkerStateSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _workers;
        private readonly IGroup<GameEntity> _tasks;

        public ValidateWarehouseWorkerStateSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _workers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseWorker,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseWorkerStoreEntityId,
                    GameMatcher.WarehouseWorkerStatus)
                .NoneOf(GameMatcher.Destructed));
            _tasks = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseTaskStoreEntityId,
                    GameMatcher.WarehouseTaskStep,
                    GameMatcher.WarehouseTaskBlockReason,
                    GameMatcher.WarehouseTaskTimeoutRemaining)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity worker in _workers)
                ValidateWorker(worker);
            foreach (GameEntity task in _tasks)
                ValidateTask(task);
        }

        private void ValidateWorker(GameEntity worker)
        {
            GameEntity task = _gameContext.GetEntityWithAssignedWorkerEntityId(
                worker.EntityId);
            bool moving = worker.WarehouseWorkerStatus is
                WarehouseWorkerStatusId.MovingToPickup or
                WarehouseWorkerStatusId.MovingToStorage or
                WarehouseWorkerStatusId.MovingToCustomerLoading or
                WarehouseWorkerStatusId.MovingToWorkerTrolley or
                WarehouseWorkerStatusId.MovingWorkerTrolleyToPickup or
                WarehouseWorkerStatusId.MovingWorkerTrolleyToStorage or
                WarehouseWorkerStatusId.MovingWorkerTrolleyToCustomerLoading;
            if (moving != (task != null))
            {
                throw new InvalidOperationException(
                    $"Warehouse worker {worker.EntityId} status/task relation diverged.");
            }
            if (task != null && ExpectedStatus(task) !=
                worker.WarehouseWorkerStatus)
            {
                throw new InvalidOperationException(
                    $"Warehouse worker {worker.EntityId} status does not match task " +
                    $"{task.EntityId} step {task.WarehouseTaskStep}.");
            }
            if (worker.isCarryingProduct && worker.isPushingWorkerTrolley ||
                worker.isHandsOccupied !=
                (worker.isCarryingProduct || worker.isPushingWorkerTrolley))
            {
                throw new InvalidOperationException(
                    $"Warehouse worker {worker.EntityId} handling markers diverged.");
            }

            GameEntity carried = _gameContext.GetEntityWithCarrierEntityId(
                worker.EntityId);
            if (worker.isCarryingProduct != (carried != null))
            {
                throw new InvalidOperationException(
                    $"Warehouse worker {worker.EntityId} carrier relation diverged.");
            }
            GameEntity pushed = _gameContext.GetEntityWithTrolleyPusherEntityId(
                worker.EntityId);
            if (worker.isPushingWorkerTrolley != (pushed != null))
                throw new InvalidOperationException(
                    $"Warehouse worker {worker.EntityId} pusher relation diverged.");
            if (pushed != null &&
                (!pushed.isWorkerTrolley || !pushed.isPlatformTrolley))
                throw new InvalidOperationException(
                    $"Warehouse worker {worker.EntityId} pushes a non-worker trolley.");
            if (worker.WarehouseWorkerStatus ==
                WarehouseWorkerStatusId.ReturningWorkerTrolley &&
                (task != null || pushed == null ||
                 !worker.isPushingWorkerTrolley || !worker.isHandsOccupied))
                throw new InvalidOperationException(
                    $"Warehouse worker {worker.EntityId} has invalid return state.");
            if (worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.Blocked &&
                task != null)
            {
                throw new InvalidOperationException(
                    $"Blocked warehouse worker {worker.EntityId} still owns a task.");
            }
        }

        private void ValidateTask(GameEntity task)
        {
            ValidateRoleAndShape(task);
            float timeout = task.WarehouseTaskTimeoutRemaining;
            if (float.IsNaN(timeout) || float.IsInfinity(timeout) || timeout < 0f)
            {
                throw new InvalidOperationException(
                    $"Warehouse task {task.EntityId} has invalid timeout {timeout}.");
            }
            if (task.WarehouseTaskStep == WarehouseTaskStepId.Blocked)
            {
                if (task.WarehouseTaskBlockReason ==
                    WarehouseTaskBlockReasonId.None)
                {
                    throw InvalidTask(task);
                }
            }
            else if (task.WarehouseTaskBlockReason !=
                     WarehouseTaskBlockReasonId.None)
            {
                throw InvalidTask(task);
            }

            if (task.isWorkerTrolleyCustomerLoadingRun)
            {
                ValidateWorkerTrolleyRun(task);
                return;
            }

            GameEntity product = _gameContext.GetEntityWithEntityId(
                task.WarehouseTaskProductEntityId);
            if (product == null || !product.isProduct || product.isDestructed ||
                !product.hasEntityId || !product.hasProductType)
            {
                throw new InvalidOperationException(
                    $"Warehouse task {task.EntityId} references invalid product.");
            }

            if (task.isInboundToStorageTask)
                ValidateInboundTask(task, product);
            else
                ValidateCustomerLoadingTask(task, product);
        }

        private static void ValidateRoleAndShape(GameEntity task)
        {
            int roleCount = (task.isInboundToStorageTask ? 1 : 0) +
                            (task.isStockToCustomerLoadingTask ? 1 : 0) +
                            (task.isWorkerTrolleyCustomerLoadingRun ? 1 : 0);
            if (roleCount != 1)
            {
                throw new InvalidOperationException(
                    $"Warehouse task {task.EntityId} must have exactly one task role.");
            }
            if (task.isWorkerTrolleyCustomerLoadingRun)
            {
                if (task.hasWarehouseTaskProductEntityId ||
                    task.hasWarehouseTaskStorageZoneEntityId ||
                    task.hasWarehouseTaskOrderLineEntityId ||
                    task.hasWarehouseTaskReservedStorageSlotIndex ||
                    task.hasWarehouseTaskReservedLoadingSlotIndex ||
                    !task.hasWarehouseTaskCustomerVisitEntityId ||
                    !task.hasWarehouseTaskWorkerTrolleyEntityId &&
                    task.WarehouseTaskStep != WarehouseTaskStepId.Blocked ||
                    !task.hasWarehouseRunProductCount)
                    throw InvalidTask(task);
                return;
            }
            if (!task.hasWarehouseTaskProductEntityId)
                throw InvalidTask(task);
            if (task.isInboundToStorageTask)
            {
                if (!task.hasWarehouseTaskStorageZoneEntityId ||
                    task.hasWarehouseTaskCustomerVisitEntityId ||
                    task.hasWarehouseTaskOrderLineEntityId ||
                    task.hasWarehouseTaskReservedLoadingSlotIndex)
                {
                    throw InvalidTask(task);
                }
                if (task.isWorkerTrolleyInboundStorageRun)
                {
                    if (!task.hasWarehouseTaskReservedStorageSlotIndex ||
                        !task.hasWarehouseTaskWorkerTrolleyEntityId ||
                        !task.hasWarehouseRunProductCount)
                        throw InvalidTask(task);
                }
                else if (task.hasWarehouseTaskWorkerTrolleyEntityId ||
                         task.hasWarehouseRunProductCount)
                {
                    throw InvalidTask(task);
                }
                return;
            }

            if (task.hasWarehouseTaskStorageZoneEntityId ||
                task.hasWarehouseTaskReservedStorageSlotIndex ||
                !task.hasWarehouseTaskCustomerVisitEntityId ||
                !task.hasWarehouseTaskOrderLineEntityId)
            {
                throw InvalidTask(task);
            }
        }

        private void ValidateInboundTask(GameEntity task,
            GameEntity product)
        {
            InboundProductManifestValidator.Validate(_gameContext, product);
            if (!product.isInboundProduct || !product.hasDeliveryEntityId ||
                !product.hasPurchaseOrderLineEntityId)
            {
                throw InvalidTask(task);
            }
            if (product.hasWarehouseRunEntityId)
            {
                ValidateInboundWorkerTrolleyTask(task, product);
                return;
            }
            if (task.isWorkerTrolleyInboundStorageRun)
                throw InvalidTask(task);
            switch (task.WarehouseTaskStep)
            {
                case WarehouseTaskStepId.Available:
                    if (task.hasAssignedWorkerEntityId ||
                        !task.hasWarehouseTaskReservedStorageSlotIndex ||
                        product.isInteractable || product.hasCarrierEntityId)
                    {
                        throw InvalidTask(task);
                    }
                    break;
                case WarehouseTaskStepId.MovingToPickup:
                    if (!task.hasAssignedWorkerEntityId ||
                        !task.hasWarehouseTaskReservedStorageSlotIndex ||
                        product.isInteractable || product.hasCarrierEntityId)
                    {
                        throw InvalidTask(task);
                    }
                    break;
                case WarehouseTaskStepId.MovingToStorage:
                    if (!task.hasAssignedWorkerEntityId ||
                        !task.hasWarehouseTaskReservedStorageSlotIndex ||
                        product.isInteractable || !product.hasCarrierEntityId)
                    {
                        throw InvalidTask(task);
                    }
                    break;
                case WarehouseTaskStepId.Blocked:
                    if (task.hasAssignedWorkerEntityId ||
                        task.hasWarehouseTaskReservedStorageSlotIndex ||
                        !product.isInteractable || !product.isInboundProduct ||
                        !product.hasDeliverySlotIndex ||
                        product.hasCarrierEntityId)
                    {
                        throw InvalidTask(task);
                    }
                    break;
                default:
                    throw InvalidTask(task);
            }
        }

        private void ValidateInboundWorkerTrolleyTask(GameEntity task,
            GameEntity product)
        {
            GameEntity run = _gameContext.GetEntityWithEntityId(
                product.WarehouseRunEntityId);
            if (run == null || run.isDestructed ||
                !run.isWarehouseTask || !run.isInboundToStorageTask ||
                !run.isWorkerTrolleyInboundStorageRun ||
                run.isWorkerTrolleyCustomerLoadingRun ||
                !run.hasEntityId ||
                !run.hasWarehouseTaskStoreEntityId ||
                !run.hasWarehouseTaskStorageZoneEntityId ||
                !run.hasWarehouseTaskWorkerTrolleyEntityId ||
                !run.hasWarehouseRunProductCount ||
                (task == run) != task.isWorkerTrolleyInboundStorageRun ||
                task.WarehouseTaskStoreEntityId !=
                run.WarehouseTaskStoreEntityId ||
                task.WarehouseTaskStorageZoneEntityId !=
                run.WarehouseTaskStorageZoneEntityId)
            {
                throw InvalidTask(task);
            }
            ValidateInboundWorkerTrolleyRun(run);
        }

        private void ValidateInboundWorkerTrolleyRun(GameEntity run)
        {
            bool available = run.WarehouseTaskStep == WarehouseTaskStepId.Available;
            bool movingToTrolley = run.WarehouseTaskStep ==
                WarehouseTaskStepId.MovingToWorkerTrolley;
            bool movingToPickup = run.WarehouseTaskStep ==
                WarehouseTaskStepId.MovingWorkerTrolleyToPickup;
            bool movingToStorage = run.WarehouseTaskStep ==
                WarehouseTaskStepId.MovingWorkerTrolleyToStorage;
            bool blocked = run.WarehouseTaskStep == WarehouseTaskStepId.Blocked;
            if (!available && !movingToTrolley && !movingToPickup &&
                !movingToStorage && !blocked)
                throw InvalidTask(run);
            if ((available || blocked) && run.hasAssignedWorkerEntityId ||
                (movingToTrolley || movingToPickup || movingToStorage) &&
                !run.hasAssignedWorkerEntityId ||
                run.WarehouseRunProductCount < 1)
                throw InvalidTask(run);

            GameEntity trolley = run.hasWarehouseTaskWorkerTrolleyEntityId
                ? _gameContext.GetEntityWithEntityId(
                    run.WarehouseTaskWorkerTrolleyEntityId)
                : null;
            bool validTrolley = trolley != null && !trolley.isDestructed &&
                trolley.isWorkerTrolley && trolley.isPlatformTrolley &&
                !trolley.isInteractable && trolley.hasEntityId &&
                trolley.hasTrolleyStoreEntityId &&
                trolley.TrolleyStoreEntityId == run.WarehouseTaskStoreEntityId &&
                trolley.hasWorkerTrolleyStoreEntityId &&
                trolley.WorkerTrolleyStoreEntityId ==
                run.WarehouseTaskStoreEntityId &&
                trolley.hasTrolleyCapacity && trolley.TrolleyCapacity > 0 &&
                trolley.hasOccupiedTrolleySlotCount;
            if (!blocked && !validTrolley ||
                validTrolley &&
                run.WarehouseRunProductCount > trolley.TrolleyCapacity)
                throw InvalidTask(run);

            GameEntity storageZone = _gameContext.GetEntityWithEntityId(
                run.WarehouseTaskStorageZoneEntityId);
            if (!blocked &&
                (storageZone == null || storageZone.isDestructed ||
                 !storageZone.isStorageZone || !storageZone.hasSlots))
                throw InvalidTask(run);

            int productCount = 0;
            var storageSlots = new HashSet<int>();
            var trolleySlots = new HashSet<int>();
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithWarehouseRunEntityId(run.EntityId))
            {
                productCount++;
                InboundProductManifestValidator.Validate(_gameContext, product);
                GameEntity task =
                    _gameContext.GetEntityWithWarehouseTaskProductEntityId(
                        product.EntityId);
                if (task == null || task.isDestructed ||
                    !task.isWarehouseTask || !task.isInboundToStorageTask ||
                    !task.hasWarehouseTaskStoreEntityId ||
                    task.WarehouseTaskStoreEntityId !=
                    run.WarehouseTaskStoreEntityId ||
                    !task.hasWarehouseTaskStorageZoneEntityId ||
                    task.WarehouseTaskStorageZoneEntityId !=
                    run.WarehouseTaskStorageZoneEntityId ||
                    !task.hasWarehouseTaskReservedStorageSlotIndex ||
                    (task == run) != task.isWorkerTrolleyInboundStorageRun ||
                    task != run &&
                    (task.hasAssignedWorkerEntityId ||
                     task.WarehouseTaskStep != WarehouseTaskStepId.Available ||
                     task.WarehouseTaskBlockReason !=
                     WarehouseTaskBlockReasonId.None) ||
                    !storageSlots.Add(
                        task.WarehouseTaskReservedStorageSlotIndex))
                {
                    throw InvalidTask(run);
                }
                if (!blocked &&
                    (task.WarehouseTaskReservedStorageSlotIndex < 0 ||
                     task.WarehouseTaskReservedStorageSlotIndex >=
                     storageZone.Slots.Length))
                    throw InvalidTask(run);

                bool onTrolley = product.hasWorkerTrolleyEntityId &&
                    validTrolley &&
                    product.WorkerTrolleyEntityId == trolley.EntityId &&
                    product.hasWorkerTrolleySlotIndex &&
                    product.WorkerTrolleySlotIndex >= 0 &&
                    product.WorkerTrolleySlotIndex < trolley.TrolleyCapacity &&
                    trolleySlots.Add(product.WorkerTrolleySlotIndex);
                bool atDelivery = product.hasDeliverySlotIndex &&
                    !product.hasReservedDeliverySlotIndex &&
                    !product.hasWorkerTrolleyEntityId &&
                    !product.hasWorkerTrolleySlotIndex;
                bool loadedOnTrolley = !product.hasDeliverySlotIndex &&
                    product.hasReservedDeliverySlotIndex && onTrolley;
                if (product.isDestructed || !product.isProduct ||
                    !product.isInboundProduct || product.isInStock ||
                    product.isLoaded || product.isInteractable ||
                    product.hasStorageZoneEntityId ||
                    product.hasStorageSlotIndex ||
                    product.hasReservedStorageSlotIndex ||
                    product.hasCarrierEntityId || product.hasOrderLineEntityId ||
                    product.hasLoadingSlotIndex || product.isLooseProduct ||
                    product.hasTrolleyEntityId || product.hasTrolleySlotIndex ||
                    movingToStorage != loadedOnTrolley ||
                    !movingToStorage && !atDelivery)
                {
                    throw InvalidTask(run);
                }
            }

            if (productCount != run.WarehouseRunProductCount)
                throw InvalidTask(run);
            if (movingToPickup || movingToStorage)
            {
                if (!trolley.hasTrolleyPusherEntityId ||
                    trolley.TrolleyPusherEntityId !=
                    run.AssignedWorkerEntityId ||
                    trolley.OccupiedTrolleySlotCount !=
                    (movingToStorage ? productCount : 0))
                    throw InvalidTask(run);
            }
            else if (!blocked && trolley.OccupiedTrolleySlotCount != 0)
            {
                throw InvalidTask(run);
            }
        }

        private void ValidateCustomerLoadingTask(GameEntity task,
            GameEntity product)
        {
            GameEntity visit = _gameContext.GetEntityWithEntityId(
                task.WarehouseTaskCustomerVisitEntityId);
            GameEntity line = _gameContext.GetEntityWithEntityId(
                task.WarehouseTaskOrderLineEntityId);
            ValidateCustomerTarget(task, visit, line, product);

            switch (task.WarehouseTaskStep)
            {
                case WarehouseTaskStepId.Available:
                    if (task.hasAssignedWorkerEntityId ||
                        !task.hasWarehouseTaskReservedLoadingSlotIndex ||
                        !HasReservedShelfState(product, line) ||
                        product.hasCarrierEntityId)
                    {
                        throw InvalidTask(task);
                    }
                    break;
                case WarehouseTaskStepId.MovingToPickup:
                    if (!task.hasAssignedWorkerEntityId ||
                        !task.hasWarehouseTaskReservedLoadingSlotIndex ||
                        !HasReservedShelfState(product, line) ||
                        product.hasCarrierEntityId)
                    {
                        throw InvalidTask(task);
                    }
                    break;
                case WarehouseTaskStepId.MovingToCustomerLoading:
                    if (!task.hasAssignedWorkerEntityId ||
                        !task.hasWarehouseTaskReservedLoadingSlotIndex ||
                        !HasReservedShelfState(product, line) ||
                        !product.hasCarrierEntityId)
                    {
                        throw InvalidTask(task);
                    }
                    break;
                case WarehouseTaskStepId.Blocked:
                    if (task.hasAssignedWorkerEntityId ||
                        task.hasWarehouseTaskReservedLoadingSlotIndex ||
                        !HasRestoredShelfState(product))
                    {
                        throw InvalidTask(task);
                    }
                    break;
                default:
                    throw InvalidTask(task);
            }

            if (task.hasWarehouseTaskReservedLoadingSlotIndex)
                ValidateLoadingSlot(task, visit);
        }

        private static void ValidateCustomerTarget(GameEntity task,
            GameEntity visit, GameEntity line, GameEntity product)
        {
            if (visit == null || visit.isDestructed ||
                !visit.isCustomerVisit || !visit.isCustomerVehicle ||
                !visit.isCustomerVisitLoading || !visit.isOrder ||
                !visit.hasEntityId ||
                visit.EntityId != task.WarehouseTaskCustomerVisitEntityId ||
                !visit.hasCustomerVisitStoreEntityId ||
                visit.CustomerVisitStoreEntityId !=
                task.WarehouseTaskStoreEntityId || !visit.hasSlots ||
                line == null || line.isDestructed || !line.isOrderLine ||
                !line.hasEntityId ||
                line.EntityId != task.WarehouseTaskOrderLineEntityId ||
                !line.hasOrderEntityId || line.OrderEntityId != visit.EntityId ||
                !line.hasStorageZoneEntityId || !line.hasProductType ||
                !line.hasRequiredProductCount || !line.hasLoadedProductCount ||
                line.RequiredProductCount <= 0 || line.LoadedProductCount < 0 ||
                line.LoadedProductCount >= line.RequiredProductCount ||
                product.ProductType != line.ProductType)
            {
                throw InvalidTask(task);
            }
        }

        private static bool HasReservedShelfState(GameEntity product,
            GameEntity line) =>
            product.isInStock && !product.isInboundProduct && !product.isLoaded &&
            !product.isInteractable && product.hasStorageZoneEntityId &&
            product.StorageZoneEntityId == line.StorageZoneEntityId &&
            !product.hasStorageSlotIndex && product.hasReservedStorageSlotIndex &&
            product.hasReservedOrderLineEntityId &&
            product.ReservedOrderLineEntityId == line.EntityId &&
            !product.hasOrderLineEntityId && !product.hasLoadingSlotIndex &&
            !product.isLooseProduct && !product.hasTrolleyEntityId &&
            !product.hasTrolleySlotIndex &&
            !product.hasWorkerTrolleyEntityId &&
            !product.hasWorkerTrolleySlotIndex &&
            !product.hasWarehouseRunEntityId &&
            !product.hasReservedCustomerLoadingSlotIndex;

        private static bool HasRestoredShelfState(GameEntity product) =>
            product.isInStock && !product.isInboundProduct && !product.isLoaded &&
            product.isInteractable && product.hasStorageZoneEntityId &&
            product.hasStorageSlotIndex && !product.hasReservedStorageSlotIndex &&
            !product.hasReservedOrderLineEntityId && !product.hasCarrierEntityId &&
            !product.hasOrderLineEntityId && !product.hasLoadingSlotIndex &&
            !product.isLooseProduct && !product.hasTrolleyEntityId &&
            !product.hasTrolleySlotIndex &&
            !product.hasWorkerTrolleyEntityId &&
            !product.hasWorkerTrolleySlotIndex &&
            !product.hasWarehouseRunEntityId &&
            !product.hasReservedCustomerLoadingSlotIndex;

        private void ValidateWorkerTrolleyRun(GameEntity run)
        {
            bool movingToTrolley = run.WarehouseTaskStep ==
                WarehouseTaskStepId.MovingToWorkerTrolley;
            bool movingToStorage = run.WarehouseTaskStep ==
                WarehouseTaskStepId.MovingWorkerTrolleyToStorage;
            bool movingToCustomer = run.WarehouseTaskStep ==
                WarehouseTaskStepId.MovingWorkerTrolleyToCustomerLoading;
            bool available = run.WarehouseTaskStep == WarehouseTaskStepId.Available;
            bool blocked = run.WarehouseTaskStep == WarehouseTaskStepId.Blocked;
            if (!movingToTrolley && !movingToStorage && !movingToCustomer &&
                !available && !blocked)
                throw InvalidTask(run);
            GameEntity visit = _gameContext.GetEntityWithEntityId(
                run.WarehouseTaskCustomerVisitEntityId);
            GameEntity trolley = run.hasWarehouseTaskWorkerTrolleyEntityId
                ? _gameContext.GetEntityWithEntityId(
                    run.WarehouseTaskWorkerTrolleyEntityId)
                : null;
            bool validActiveVisit = visit != null && !visit.isDestructed &&
                visit.isCustomerVisit && visit.isCustomerVisitLoading &&
                visit.isOrder && visit.hasEntityId && visit.hasSlots;
            bool validTrolley = trolley != null && !trolley.isDestructed &&
                trolley.isWorkerTrolley && trolley.isPlatformTrolley &&
                !trolley.isInteractable && trolley.hasEntityId &&
                trolley.hasTrolleyStoreEntityId &&
                trolley.TrolleyStoreEntityId == run.WarehouseTaskStoreEntityId &&
                trolley.hasWorkerTrolleyStoreEntityId &&
                trolley.WorkerTrolleyStoreEntityId ==
                run.WarehouseTaskStoreEntityId &&
                trolley.hasTrolleyCapacity &&
                trolley.hasOccupiedTrolleySlotCount;
            if (!blocked && (!validActiveVisit || !validTrolley) ||
                run.WarehouseRunProductCount < 1 ||
                validTrolley &&
                run.WarehouseRunProductCount > trolley.TrolleyCapacity ||
                validActiveVisit && visit.Slots.Length > 64 ||
                validTrolley && trolley.TrolleyCapacity > 64)
                throw InvalidTask(run);
            if ((available || blocked) && run.hasAssignedWorkerEntityId ||
                (movingToTrolley || movingToStorage || movingToCustomer) &&
                !run.hasAssignedWorkerEntityId)
                throw InvalidTask(run);

            int productCount = 0;
            ulong loadingSlots = 0;
            ulong trolleySlots = 0;
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithWarehouseRunEntityId(run.EntityId))
            {
                productCount++;
                if (blocked)
                {
                    if (product.isDestructed || !product.isProduct ||
                        !product.isInStock || product.isInboundProduct ||
                        product.isLoaded || !product.isInteractable ||
                        !product.hasStorageZoneEntityId ||
                        !product.hasStorageSlotIndex ||
                        product.hasReservedStorageSlotIndex ||
                        product.hasReservedOrderLineEntityId ||
                        product.hasReservedCustomerLoadingSlotIndex ||
                        product.hasWorkerTrolleyEntityId ||
                        product.hasWorkerTrolleySlotIndex ||
                        product.hasCarrierEntityId || product.hasOrderLineEntityId ||
                        product.hasLoadingSlotIndex)
                        throw InvalidTask(run);
                    continue;
                }

                if (product.isDestructed || !product.isProduct ||
                    !product.isInStock || product.isInboundProduct ||
                    product.isLoaded || product.isInteractable ||
                    !product.hasStorageZoneEntityId ||
                    !product.hasReservedStorageSlotIndex ||
                    !product.hasReservedOrderLineEntityId ||
                    !product.hasReservedCustomerLoadingSlotIndex ||
                    product.ReservedCustomerLoadingSlotIndex < 0 ||
                    product.ReservedCustomerLoadingSlotIndex >= visit.Slots.Length ||
                    product.hasStorageSlotIndex || product.hasCarrierEntityId ||
                    product.hasOrderLineEntityId || product.hasLoadingSlotIndex ||
                    product.hasTrolleyEntityId || product.hasTrolleySlotIndex)
                    throw InvalidTask(run);
                ulong loadingBit = 1UL <<
                    product.ReservedCustomerLoadingSlotIndex;
                if ((loadingSlots & loadingBit) != 0)
                    throw InvalidTask(run);
                loadingSlots |= loadingBit;

                bool exactCartPlacement = product.hasWorkerTrolleyEntityId &&
                    product.WorkerTrolleyEntityId == trolley.EntityId &&
                    product.hasWorkerTrolleySlotIndex &&
                    product.WorkerTrolleySlotIndex >= 0 &&
                    product.WorkerTrolleySlotIndex < trolley.TrolleyCapacity;
                if (movingToCustomer != exactCartPlacement)
                    throw InvalidTask(run);
                if (!movingToCustomer &&
                    (product.hasWorkerTrolleyEntityId ||
                     product.hasWorkerTrolleySlotIndex))
                    throw InvalidTask(run);
                if (movingToCustomer)
                {
                    ulong trolleyBit = 1UL << product.WorkerTrolleySlotIndex;
                    if ((trolleySlots & trolleyBit) != 0)
                        throw InvalidTask(run);
                    trolleySlots |= trolleyBit;
                }
            }

            if (!blocked && productCount != run.WarehouseRunProductCount ||
                blocked && productCount > run.WarehouseRunProductCount)
                throw InvalidTask(run);
            if (movingToStorage || movingToCustomer)
            {
                if (!trolley.hasTrolleyPusherEntityId ||
                    trolley.TrolleyPusherEntityId !=
                    run.AssignedWorkerEntityId ||
                    trolley.OccupiedTrolleySlotCount !=
                    (movingToCustomer ? productCount : 0))
                    throw InvalidTask(run);
            }
            else if (blocked && validTrolley &&
                     (trolley.hasTrolleyPusherEntityId ||
                      trolley.OccupiedTrolleySlotCount != 0))
                throw InvalidTask(run);
        }

        private void ValidateLoadingSlot(GameEntity ownerTask, GameEntity visit)
        {
            int slotIndex = ownerTask.WarehouseTaskReservedLoadingSlotIndex;
            if (slotIndex < 0 || slotIndex >= visit.Slots.Length)
                throw InvalidTask(ownerTask);
            foreach (GameEntity task in
                     _gameContext.GetEntitiesWithWarehouseTaskCustomerVisitEntityId(
                         visit.EntityId))
            {
                if (task == ownerTask || task.isDestructed ||
                    !task.hasWarehouseTaskReservedLoadingSlotIndex)
                {
                    continue;
                }
                if (!task.isStockToCustomerLoadingTask ||
                    task.WarehouseTaskReservedLoadingSlotIndex == slotIndex)
                {
                    throw InvalidTask(ownerTask);
                }
            }
            foreach (GameEntity line in
                     _gameContext.GetEntitiesWithOrderEntityId(visit.EntityId))
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithOrderLineEntityId(line.EntityId))
            {
                if (!product.isDestructed && product.isLoaded &&
                    product.hasLoadingSlotIndex &&
                    product.LoadingSlotIndex == slotIndex)
                {
                    throw InvalidTask(ownerTask);
                }
            }
        }

        private static WarehouseWorkerStatusId ExpectedStatus(GameEntity task) =>
            task.WarehouseTaskStep switch
            {
                WarehouseTaskStepId.MovingToPickup =>
                    WarehouseWorkerStatusId.MovingToPickup,
                WarehouseTaskStepId.MovingToStorage =>
                    WarehouseWorkerStatusId.MovingToStorage,
                WarehouseTaskStepId.MovingToCustomerLoading =>
                    WarehouseWorkerStatusId.MovingToCustomerLoading,
                WarehouseTaskStepId.MovingToWorkerTrolley =>
                    WarehouseWorkerStatusId.MovingToWorkerTrolley,
                WarehouseTaskStepId.MovingWorkerTrolleyToPickup =>
                    WarehouseWorkerStatusId.MovingWorkerTrolleyToPickup,
                WarehouseTaskStepId.MovingWorkerTrolleyToStorage =>
                    WarehouseWorkerStatusId.MovingWorkerTrolleyToStorage,
                WarehouseTaskStepId.MovingWorkerTrolleyToCustomerLoading =>
                    WarehouseWorkerStatusId.MovingWorkerTrolleyToCustomerLoading,
                _ => throw InvalidTask(task)
            };

        private static InvalidOperationException InvalidTask(GameEntity task) =>
            new($"Warehouse task {task.EntityId} has invalid " +
                $"{task.WarehouseTaskStep} state.");
    }
}
