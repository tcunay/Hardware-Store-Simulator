using System;
using Entitas;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class SyncWarehouseWorkerShiftSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _workers;

        public SyncWarehouseWorkerShiftSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _workers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseWorker,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseWorkerStoreEntityId,
                    GameMatcher.WorkerPaidDayNumber,
                    GameMatcher.WarehouseWorkerStatus)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity worker in _workers)
            {
                GameEntity store = _gameContext.GetEntityWithEntityId(
                    worker.WarehouseWorkerStoreEntityId);
                if (store == null || !store.isStore || store.isDestructed ||
                    !store.hasDayNumber || worker.WorkerPaidDayNumber <= 0 ||
                    worker.WorkerPaidDayNumber > store.DayNumber)
                {
                    throw new InvalidOperationException(
                        $"Warehouse worker {worker.EntityId} has an invalid paid-day relation.");
                }

                bool paidForCurrentDay = worker.WorkerPaidDayNumber == store.DayNumber;
                if (paidForCurrentDay)
                {
                    if (!worker.isWorkerShiftActive ||
                        worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.OffShift)
                    {
                        throw new InvalidOperationException(
                            $"Paid warehouse worker {worker.EntityId} is not on shift.");
                    }

                    continue;
                }

                if (worker.isWorkerShiftActive)
                    worker.isWorkerShiftActive = false;
                if (worker.WarehouseWorkerStatus != WarehouseWorkerStatusId.OffShift)
                    worker.ReplaceWarehouseWorkerStatus(WarehouseWorkerStatusId.OffShift);
            }
        }
    }
}
