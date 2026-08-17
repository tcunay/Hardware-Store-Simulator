using System;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class AssignWarehouseTaskSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly WarehouseWorkerConfig _config;
        private readonly IGroup<GameEntity> _workers;
        private readonly IGroup<GameEntity> _tasks;

        public AssignWarehouseTaskSystem(GameContext gameContext,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _config = staticData.WarehouseWorker;
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
                    GameMatcher.WarehouseWorkerPickupPosition,
                    GameMatcher.WarehouseWorkerPickupRotation,
                    GameMatcher.WarehouseWorkerStoragePosition,
                    GameMatcher.WarehouseWorkerStorageRotation,
                    GameMatcher.WarehouseWorkerCustomerLoadingPosition,
                    GameMatcher.WarehouseWorkerCustomerLoadingRotation)
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
            {
                bool storageFull = worker.WarehouseWorkerStatus ==
                    WarehouseWorkerStatusId.StorageFull;
                bool returningTrolley = worker.WarehouseWorkerStatus ==
                    WarehouseWorkerStatusId.ReturningWorkerTrolley;
                if (worker.WarehouseWorkerStatus != WarehouseWorkerStatusId.Idle &&
                    !storageFull && !returningTrolley)
                    continue;
                if (_gameContext.GetEntityWithAssignedWorkerEntityId(
                        worker.EntityId) != null)
                {
                    throw new InvalidOperationException(
                        $"Idle warehouse worker {worker.EntityId} owns an assigned task.");
                }

                GameEntity store = _gameContext.GetEntityWithEntityId(
                    worker.WarehouseWorkerStoreEntityId);
                ValidateStore(worker, store);
                if (!store.isStoreOpen && !store.isStoreClosing)
                    continue;

                GameEntity selected = FindTask(store.EntityId);
                if (selected == null)
                    continue;
                if (returningTrolley &&
                    !selected.isWorkerTrolleyCustomerLoadingRun)
                {
                    continue;
                }
                if (storageFull && !selected.isStockToCustomerLoadingTask &&
                    !selected.isWorkerTrolleyCustomerLoadingRun)
                    continue;
                if (storageFull)
                    worker.ReplaceWarehouseWorkerStatus(WarehouseWorkerStatusId.Idle);

                selected.AddAssignedWorkerEntityId(worker.EntityId);
                bool trolleyRun = selected.isWorkerTrolleyCustomerLoadingRun;
                selected.ReplaceWarehouseTaskStep(trolleyRun
                    ? WarehouseTaskStepId.MovingToWorkerTrolley
                    : WarehouseTaskStepId.MovingToPickup);
                selected.ReplaceWarehouseTaskBlockReason(
                    WarehouseTaskBlockReasonId.None);
                selected.ReplaceWarehouseTaskTimeoutRemaining(
                    _config.TaskTimeout);
                worker.ReplaceWarehouseWorkerStatus(trolleyRun
                    ? WarehouseWorkerStatusId.MovingToWorkerTrolley
                    : WarehouseWorkerStatusId.MovingToPickup);
            }
        }

        private GameEntity FindTask(int storeEntityId)
        {
            GameEntity selected = null;
            foreach (GameEntity task in _tasks)
            {
                ValidateRole(task);
                if (task.WarehouseTaskStoreEntityId != storeEntityId ||
                    task.WarehouseTaskStep != WarehouseTaskStepId.Available ||
                    task.hasAssignedWorkerEntityId)
                {
                    continue;
                }

                if (selected == null || ComparePriority(task, selected) < 0)
                    selected = task;
            }

            return selected;
        }

        private static int ComparePriority(GameEntity left, GameEntity right)
        {
            int leftPriority = left.isStockToCustomerLoadingTask ||
                               left.isWorkerTrolleyCustomerLoadingRun ? 0 : 1;
            int rightPriority = right.isStockToCustomerLoadingTask ||
                                right.isWorkerTrolleyCustomerLoadingRun ? 0 : 1;
            int priorityComparison = leftPriority.CompareTo(rightPriority);
            return priorityComparison != 0
                ? priorityComparison
                : left.EntityId.CompareTo(right.EntityId);
        }

        private static void ValidateRole(GameEntity task)
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
                    !task.hasWarehouseTaskCustomerVisitEntityId ||
                    !task.hasWarehouseTaskWorkerTrolleyEntityId ||
                    !task.hasWarehouseRunProductCount)
                {
                    throw new InvalidOperationException(
                        $"Worker-trolley run {task.EntityId} has invalid relations.");
                }
            }
            else if (!task.hasWarehouseTaskProductEntityId)
            {
                throw new InvalidOperationException(
                    $"Warehouse task {task.EntityId} has no product relation.");
            }
        }

        private static void ValidateStore(GameEntity worker, GameEntity store)
        {
            if (store == null || !store.isStore || store.isDestructed ||
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
