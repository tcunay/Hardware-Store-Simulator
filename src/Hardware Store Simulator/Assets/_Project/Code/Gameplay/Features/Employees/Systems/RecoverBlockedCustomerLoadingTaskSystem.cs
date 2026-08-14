using System;
using Entitas;
using HardwareStore.Gameplay.Common.Navigation;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class RecoverBlockedCustomerLoadingTaskSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IWorkerNavigationService _navigation;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _tasks;

        public RecoverBlockedCustomerLoadingTaskSystem(GameContext gameContext,
            IWorkerNavigationService navigation, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _navigation = navigation;
            _events = events;
            _tasks = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.StockToCustomerLoadingTask,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseTaskStoreEntityId,
                    GameMatcher.WarehouseTaskProductEntityId,
                    GameMatcher.WarehouseTaskCustomerVisitEntityId,
                    GameMatcher.WarehouseTaskOrderLineEntityId,
                    GameMatcher.WarehouseTaskStep,
                    GameMatcher.WarehouseTaskBlockReason)
                .NoneOf(
                    GameMatcher.InboundToStorageTask,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity task in _tasks)
            {
                if (task.WarehouseTaskStep != WarehouseTaskStepId.Blocked ||
                    (!task.hasAssignedWorkerEntityId &&
                     !task.hasWarehouseTaskReservedLoadingSlotIndex))
                {
                    continue;
                }
                if (task.WarehouseTaskBlockReason == WarehouseTaskBlockReasonId.None)
                {
                    throw new InvalidOperationException(
                        $"Blocked customer loading task {task.EntityId} has no reason.");
                }

                GameEntity product = _gameContext.GetEntityWithEntityId(
                    task.WarehouseTaskProductEntityId);
                GameEntity line = _gameContext.GetEntityWithEntityId(
                    task.WarehouseTaskOrderLineEntityId);
                ValidateReservedProduct(task, product, line);

                int workerEntityId = task.hasAssignedWorkerEntityId
                    ? task.AssignedWorkerEntityId
                    : 0;
                GameEntity worker = task.hasAssignedWorkerEntityId
                    ? _gameContext.GetEntityWithEntityId(workerEntityId)
                    : null;
                if (worker != null && !worker.isDestructed)
                {
                    if (!worker.isWarehouseWorker || !worker.hasNavigationAgent ||
                        !worker.hasWarehouseWorkerStatus)
                    {
                        throw new InvalidOperationException(
                            $"Task {task.EntityId} references invalid worker " +
                            $"{workerEntityId}.");
                    }
                    _navigation.Stop(worker.NavigationAgent);
                    worker.isHandsOccupied = false;
                    worker.isCarryingProduct = false;
                    worker.ReplaceWarehouseWorkerStatus(
                        WarehouseWorkerStatusId.Blocked);
                }

                RestoreProductToShelf(product, workerEntityId);
                if (task.hasAssignedWorkerEntityId)
                    task.RemoveAssignedWorkerEntityId();
                if (task.hasWarehouseTaskReservedLoadingSlotIndex)
                    task.RemoveWarehouseTaskReservedLoadingSlotIndex();
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationWarehouseWorkerTaskBlocked,
                    LocalizedTexts.ProductName(product.ProductType)));
            }
        }

        private void ValidateReservedProduct(GameEntity task, GameEntity product,
            GameEntity line)
        {
            if (line == null || line.isDestructed || !line.isOrderLine ||
                !line.hasEntityId ||
                line.EntityId != task.WarehouseTaskOrderLineEntityId ||
                !line.hasOrderEntityId ||
                line.OrderEntityId != task.WarehouseTaskCustomerVisitEntityId ||
                !line.hasStorageZoneEntityId || !line.hasProductType ||
                !line.hasRequiredProductCount || !line.hasLoadedProductCount)
            {
                throw new InvalidOperationException(
                    $"Blocked customer task {task.EntityId} has invalid order line.");
            }
            if (product == null || product.isDestructed || !product.isProduct ||
                !product.hasEntityId ||
                product.EntityId != task.WarehouseTaskProductEntityId ||
                !product.hasProductType || product.ProductType != line.ProductType ||
                !product.isInStock || product.isInboundProduct || product.isLoaded ||
                !product.hasStorageZoneEntityId ||
                product.StorageZoneEntityId != line.StorageZoneEntityId ||
                !product.hasReservedStorageSlotIndex ||
                !product.hasReservedOrderLineEntityId ||
                product.ReservedOrderLineEntityId != line.EntityId ||
                product.hasStorageSlotIndex || product.hasOrderLineEntityId ||
                product.hasLoadingSlotIndex || product.hasDeliveryEntityId ||
                product.hasDeliverySlotIndex ||
                product.hasReservedDeliverySlotIndex || product.isLooseProduct ||
                product.hasTrolleyEntityId || product.hasTrolleySlotIndex ||
                product.hasWorldPosition || product.hasWorldRotation ||
                product.isInteractable)
            {
                throw new InvalidOperationException(
                    $"Blocked customer task {task.EntityId} has invalid product reservation.");
            }

            GameEntity storageZone = _gameContext.GetEntityWithEntityId(
                product.StorageZoneEntityId);
            if (storageZone == null || storageZone.isDestructed ||
                !storageZone.isStorageZone || !storageZone.hasSlots ||
                product.ReservedStorageSlotIndex < 0 ||
                product.ReservedStorageSlotIndex >= storageZone.Slots.Length)
            {
                throw new InvalidOperationException(
                    $"Blocked product {product.EntityId} has invalid storage slot.");
            }
        }

        private static void RestoreProductToShelf(GameEntity product,
            int workerEntityId)
        {
            if (product.hasCarrierEntityId)
            {
                if (workerEntityId == 0 ||
                    product.CarrierEntityId != workerEntityId)
                {
                    throw new InvalidOperationException(
                        $"Blocked product {product.EntityId} has invalid carrier relation.");
                }
                product.RemoveCarrierEntityId();
            }

            int storageSlotIndex = product.ReservedStorageSlotIndex;
            product.RemoveReservedStorageSlotIndex();
            product.RemoveReservedOrderLineEntityId();
            product.AddStorageSlotIndex(storageSlotIndex);
            product.isInteractable = true;
            product.isProductPlacementDirty = true;
        }
    }
}
