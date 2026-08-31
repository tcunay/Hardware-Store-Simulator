using System;
using Entitas;
using HardwareStore.Gameplay.Common.Time;
using HardwareStore.Gameplay.Components;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class TickWarehouseTaskTimeoutSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly ITimeService _time;
        private readonly IGroup<GameEntity> _tasks;

        public TickWarehouseTaskTimeoutSystem(GameContext gameContext,
            ITimeService time)
        {
            _gameContext = gameContext;
            _time = time;
            _tasks = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.EntityId,
                    GameMatcher.AssignedWorkerEntityId,
                    GameMatcher.WarehouseTaskStep,
                    GameMatcher.WarehouseTaskBlockReason,
                    GameMatcher.WarehouseTaskTimeoutRemaining)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity task in _tasks)
            {
                int roleCount = (task.isInboundToStorageTask ? 1 : 0) +
                                (task.isStockToCustomerLoadingTask ? 1 : 0) +
                                (task.isWorkerTrolleyCustomerLoadingRun ? 1 : 0);
                if (roleCount != 1)
                {
                    throw new InvalidOperationException(
                        $"Warehouse task {task.EntityId} must have exactly one task role.");
                }
                if (task.WarehouseTaskStep == WarehouseTaskStepId.Blocked)
                    continue;
                GameEntity worker = _gameContext.GetEntityWithEntityId(
                    task.AssignedWorkerEntityId);
                if (worker != null && !worker.isDestructed &&
                    worker.isTrafficYielding &&
                    worker.hasTrafficConflictEntityId)
                    continue;
                float current = task.WarehouseTaskTimeoutRemaining;
                if (float.IsNaN(current) || float.IsInfinity(current) || current < 0f)
                {
                    throw new InvalidOperationException(
                        $"Warehouse task {task.EntityId} has invalid timeout {current}.");
                }

                float remaining = Mathf.Max(0f, current - _time.DeltaTime);
                task.ReplaceWarehouseTaskTimeoutRemaining(remaining);
                if (remaining > 0f)
                    continue;

                task.ReplaceWarehouseTaskStep(WarehouseTaskStepId.Blocked);
                task.ReplaceWarehouseTaskBlockReason(
                    WarehouseTaskBlockReasonId.TimedOut);
            }
        }
    }
}
