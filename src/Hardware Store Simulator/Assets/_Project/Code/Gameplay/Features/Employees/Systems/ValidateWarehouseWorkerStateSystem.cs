using System;
using Entitas;
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
                GameEntity task = _gameContext.GetEntityWithAssignedWorkerEntityId(
                    worker.EntityId);
                bool moving = worker.WarehouseWorkerStatus ==
                                  WarehouseWorkerStatusId.MovingToPickup ||
                              worker.WarehouseWorkerStatus ==
                                  WarehouseWorkerStatusId.MovingToStorage;
                if (moving != (task != null))
                    throw new InvalidOperationException(
                        $"Warehouse worker {worker.EntityId} status/task relation diverged.");
                if (worker.isHandsOccupied != worker.isCarryingProduct)
                    throw new InvalidOperationException(
                        $"Warehouse worker {worker.EntityId} handling markers diverged.");

                GameEntity carried = _gameContext.GetEntityWithCarrierEntityId(
                    worker.EntityId);
                if (worker.isCarryingProduct != (carried != null))
                    throw new InvalidOperationException(
                        $"Warehouse worker {worker.EntityId} carrier relation diverged.");
                if (worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.Blocked &&
                    task != null)
                    throw new InvalidOperationException(
                        $"Blocked warehouse worker {worker.EntityId} still owns a task.");
            }

            foreach (GameEntity task in _tasks)
                ValidateTask(task);
        }

        private void ValidateTask(GameEntity task)
        {
            GameEntity product = _gameContext.GetEntityWithEntityId(
                task.WarehouseTaskProductEntityId);
            if (product == null || !product.isProduct || product.isDestructed)
                throw new InvalidOperationException(
                    $"Warehouse task {task.EntityId} references invalid product.");

            switch (task.WarehouseTaskStep)
            {
                case WarehouseTaskStepId.Available:
                    if (task.hasAssignedWorkerEntityId ||
                        !task.hasWarehouseTaskReservedStorageSlotIndex ||
                        product.isInteractable)
                        throw InvalidTask(task);
                    break;
                case WarehouseTaskStepId.MovingToPickup:
                    if (!task.hasAssignedWorkerEntityId ||
                        !task.hasWarehouseTaskReservedStorageSlotIndex ||
                        product.isInteractable || product.hasCarrierEntityId)
                        throw InvalidTask(task);
                    break;
                case WarehouseTaskStepId.MovingToStorage:
                    if (!task.hasAssignedWorkerEntityId ||
                        !task.hasWarehouseTaskReservedStorageSlotIndex ||
                        product.isInteractable || !product.hasCarrierEntityId)
                        throw InvalidTask(task);
                    break;
                case WarehouseTaskStepId.Blocked:
                    if (task.hasAssignedWorkerEntityId ||
                        task.hasWarehouseTaskReservedStorageSlotIndex ||
                        !product.isInteractable ||
                        task.WarehouseTaskBlockReason == WarehouseTaskBlockReasonId.None)
                        throw InvalidTask(task);
                    break;
                default:
                    throw InvalidTask(task);
            }
        }

        private static InvalidOperationException InvalidTask(GameEntity task) =>
            new($"Warehouse task {task.EntityId} has invalid {task.WarehouseTaskStep} state.");
    }
}
