using System;
using Entitas;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class DetectOrphanedWorkerTrolleyRunSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _runs;

        public DetectOrphanedWorkerTrolleyRunSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _runs = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.WorkerTrolleyCustomerLoadingRun,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseTaskStoreEntityId,
                    GameMatcher.WarehouseTaskCustomerVisitEntityId,
                    GameMatcher.WarehouseTaskWorkerTrolleyEntityId,
                    GameMatcher.WarehouseTaskStep,
                    GameMatcher.WarehouseTaskBlockReason)
                .NoneOf(
                    GameMatcher.InboundToStorageTask,
                    GameMatcher.StockToCustomerLoadingTask,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity run in _runs)
            {
                if (run.WarehouseTaskStep == WarehouseTaskStepId.Blocked)
                    continue;
                GameEntity store = _gameContext.GetEntityWithEntityId(
                    run.WarehouseTaskStoreEntityId);
                if (store == null || store.isDestructed || !store.isStore)
                    throw new InvalidOperationException(
                        $"Worker-trolley run {run.EntityId} has invalid store.");

                GameEntity worker =
                    _gameContext.GetEntityWithWarehouseWorkerStoreEntityId(
                        store.EntityId);
                if (worker == null || worker.isDestructed ||
                    run.hasAssignedWorkerEntityId &&
                    run.AssignedWorkerEntityId != worker.EntityId)
                {
                    Block(run, WarehouseTaskBlockReasonId.WorkerMissing);
                    continue;
                }

                GameEntity trolley = _gameContext.GetEntityWithEntityId(
                    run.WarehouseTaskWorkerTrolleyEntityId);
                if (trolley == null || trolley.isDestructed ||
                    !trolley.isWorkerTrolley ||
                    !trolley.hasWorkerTrolleyStoreEntityId ||
                    trolley.WorkerTrolleyStoreEntityId != store.EntityId)
                {
                    Block(run, WarehouseTaskBlockReasonId.WorkerTrolleyMissing);
                    continue;
                }

                GameEntity visit = _gameContext.GetEntityWithEntityId(
                    run.WarehouseTaskCustomerVisitEntityId);
                if (visit == null || visit.isDestructed ||
                    !visit.isCustomerVisit || !visit.isCustomerVisitLoading ||
                    !visit.isOrder || !visit.hasCustomerVisitStoreEntityId ||
                    visit.CustomerVisitStoreEntityId != store.EntityId)
                    Block(run, WarehouseTaskBlockReasonId.NoCustomerLoadingPath);
            }
        }

        private static void Block(GameEntity run,
            WarehouseTaskBlockReasonId reason)
        {
            run.ReplaceWarehouseTaskStep(WarehouseTaskStepId.Blocked);
            run.ReplaceWarehouseTaskBlockReason(reason);
        }
    }
}
