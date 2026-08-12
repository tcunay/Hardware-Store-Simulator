using Entitas;
using HardwareStore.Gameplay.Components;
using System.Collections.Generic;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class CleanupBlockedWarehouseTaskSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _tasks;
        private readonly List<GameEntity> _buffer = new(4);

        public CleanupBlockedWarehouseTaskSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _tasks = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.InboundToStorageTask,
                    GameMatcher.WarehouseTaskStoreEntityId,
                    GameMatcher.WarehouseTaskProductEntityId,
                    GameMatcher.WarehouseTaskStep,
                    GameMatcher.WarehouseTaskBlockReason)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity task in _tasks.GetEntities(_buffer))
            {
                if (task.WarehouseTaskStep != WarehouseTaskStepId.Blocked)
                    continue;
                if (task.WarehouseTaskBlockReason == WarehouseTaskBlockReasonId.None)
                    throw new System.InvalidOperationException(
                        $"Blocked warehouse task {task.EntityId} has no reason.");

                GameEntity product = _gameContext.GetEntityWithEntityId(
                    task.WarehouseTaskProductEntityId);
                if (product != null && !product.isDestructed &&
                    product.isInboundProduct && product.hasDeliverySlotIndex &&
                    product.isInteractable)
                    continue;

                task.isDestructed = true;
                GameEntity worker = _gameContext.GetEntityWithWarehouseWorkerStoreEntityId(
                    task.WarehouseTaskStoreEntityId);
                if (worker == null || worker.isDestructed)
                    continue;
                if (!worker.isWarehouseWorker || !worker.hasWarehouseWorkerStatus)
                    throw new System.InvalidOperationException(
                        $"Blocked task {task.EntityId} references invalid worker.");
                if (worker.WarehouseWorkerStatus != WarehouseWorkerStatusId.Blocked)
                    continue;

                worker.ReplaceWarehouseWorkerStatus(worker.isWorkerShiftActive
                    ? WarehouseWorkerStatusId.Idle
                    : WarehouseWorkerStatusId.OffShift);
            }
        }
    }
}
