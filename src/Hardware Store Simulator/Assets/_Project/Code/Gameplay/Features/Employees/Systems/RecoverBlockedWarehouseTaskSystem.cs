using System;
using Entitas;
using HardwareStore.Gameplay.Common.Navigation;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class RecoverBlockedWarehouseTaskSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IWorkerNavigationService _navigation;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _tasks;

        public RecoverBlockedWarehouseTaskSystem(GameContext gameContext,
            IWorkerNavigationService navigation, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _navigation = navigation;
            _events = events;
            _tasks = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.InboundToStorageTask,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseTaskProductEntityId,
                    GameMatcher.WarehouseTaskStep,
                    GameMatcher.WarehouseTaskBlockReason)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity task in _tasks)
            {
                if (task.WarehouseTaskStep != WarehouseTaskStepId.Blocked ||
                    (!task.hasAssignedWorkerEntityId &&
                     !task.hasWarehouseTaskReservedStorageSlotIndex))
                    continue;
                if (task.WarehouseTaskBlockReason == WarehouseTaskBlockReasonId.None)
                    throw new InvalidOperationException(
                        $"Blocked warehouse task {task.EntityId} has no reason.");

                GameEntity product = _gameContext.GetEntityWithEntityId(
                    task.WarehouseTaskProductEntityId);
                if (product == null || !product.isProduct || product.isDestructed ||
                    !product.hasProductType)
                    throw new InvalidOperationException(
                        $"Blocked warehouse task {task.EntityId} lost its product.");

                int workerEntityId = task.hasAssignedWorkerEntityId
                    ? task.AssignedWorkerEntityId
                    : 0;
                GameEntity worker = task.hasAssignedWorkerEntityId
                    ? _gameContext.GetEntityWithEntityId(workerEntityId)
                    : null;
                if (worker != null && !worker.isDestructed)
                {
                    if (!worker.isWarehouseWorker || !worker.hasNavigationAgent)
                        throw new InvalidOperationException(
                            $"Task {task.EntityId} references invalid worker {workerEntityId}.");
                    _navigation.Stop(worker.NavigationAgent);
                    worker.isHandsOccupied = false;
                    worker.isCarryingProduct = false;
                    worker.ReplaceWarehouseWorkerStatus(WarehouseWorkerStatusId.Blocked);
                }

                RestoreProductToDeliverySlot(product, workerEntityId);
                if (task.hasAssignedWorkerEntityId)
                    task.RemoveAssignedWorkerEntityId();
                if (task.hasWarehouseTaskReservedStorageSlotIndex)
                    task.RemoveWarehouseTaskReservedStorageSlotIndex();
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationWarehouseWorkerTaskBlocked,
                    LocalizedTexts.ProductName(product.ProductType)));
            }
        }

        private static void RestoreProductToDeliverySlot(GameEntity product,
            int workerEntityId)
        {
            if (product.hasCarrierEntityId)
            {
                if (workerEntityId == 0 || product.CarrierEntityId != workerEntityId ||
                    !product.hasReservedDeliverySlotIndex ||
                    product.hasDeliverySlotIndex)
                    throw new InvalidOperationException(
                        $"Blocked product {product.EntityId} has invalid carry reservation.");

                int slotIndex = product.ReservedDeliverySlotIndex;
                product.RemoveCarrierEntityId();
                product.RemoveReservedDeliverySlotIndex();
                product.AddDeliverySlotIndex(slotIndex);
                product.isProductPlacementDirty = true;
            }
            else if (!product.hasDeliverySlotIndex ||
                     product.hasReservedDeliverySlotIndex)
            {
                throw new InvalidOperationException(
                    $"Blocked product {product.EntityId} cannot return to its delivery slot.");
            }

            product.isInteractable = true;
        }
    }
}
