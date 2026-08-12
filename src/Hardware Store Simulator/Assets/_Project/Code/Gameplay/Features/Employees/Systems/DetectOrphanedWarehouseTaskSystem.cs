using System;
using Entitas;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class DetectOrphanedWarehouseTaskSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _tasks;

        public DetectOrphanedWarehouseTaskSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _tasks = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.InboundToStorageTask,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseTaskStoreEntityId,
                    GameMatcher.WarehouseTaskProductEntityId,
                    GameMatcher.WarehouseTaskStep,
                    GameMatcher.WarehouseTaskBlockReason)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity task in _tasks)
            {
                if (task.WarehouseTaskStep == WarehouseTaskStepId.Blocked)
                    continue;

                GameEntity store = _gameContext.GetEntityWithEntityId(
                    task.WarehouseTaskStoreEntityId);
                if (store == null || !store.isStore || store.isDestructed)
                    throw new InvalidOperationException(
                        $"Warehouse task {task.EntityId} has invalid store relation.");

                GameEntity worker = _gameContext.GetEntityWithWarehouseWorkerStoreEntityId(
                    store.EntityId);
                bool workerMissing = worker == null || worker.isDestructed;
                if (!workerMissing && task.hasAssignedWorkerEntityId)
                    workerMissing = task.AssignedWorkerEntityId != worker.EntityId;
                if (!workerMissing)
                    continue;

                task.ReplaceWarehouseTaskStep(WarehouseTaskStepId.Blocked);
                task.ReplaceWarehouseTaskBlockReason(
                    WarehouseTaskBlockReasonId.WorkerMissing);
            }
        }
    }
}
