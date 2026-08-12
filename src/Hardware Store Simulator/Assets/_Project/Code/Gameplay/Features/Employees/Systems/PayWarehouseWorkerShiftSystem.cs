using System;
using Entitas;
using HardwareStore.Gameplay.Common.Economy;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class PayWarehouseWorkerShiftSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly WarehouseWorkerConfig _config;
        private readonly IEconomySolvencyService _solvency;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public PayWarehouseWorkerShiftSystem(GameContext gameContext,
            IStaticDataService staticData, IEconomySolvencyService solvency,
            IGameEventFactory events)
        {
            _gameContext = gameContext;
            _config = staticData.WarehouseWorker;
            _solvency = solvency;
            _events = events;
            _requests = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.InteractionRequest,
                GameMatcher.SourceEntityId,
                GameMatcher.TargetEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity request in _requests)
            {
                GameEntity terminal = _gameContext.GetEntityWithEntityId(
                    request.TargetEntityId);
                if (terminal == null)
                    throw new InvalidOperationException(
                        $"Warehouse worker payment targets missing entity " +
                        $"{request.TargetEntityId}.");
                if (!terminal.isStoreControlTerminal)
                    continue;

                GameEntity player = _gameContext.GetEntityWithEntityId(
                    request.SourceEntityId);
                GameEntity store = ValidateInteraction(player, terminal);
                if (!store.isStoreOpen || player.isModalOpen || player.isHandsOccupied)
                    continue;

                GameEntity worker = _gameContext.GetEntityWithWarehouseWorkerStoreEntityId(
                    store.EntityId);
                if (worker == null)
                    continue;
                ValidateWorker(worker, store);
                if (worker.WorkerPaidDayNumber == store.DayNumber)
                {
                    if (!worker.isWorkerShiftActive ||
                        worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.OffShift)
                    {
                        throw new InvalidOperationException(
                            $"Paid warehouse worker {worker.EntityId} is not on shift.");
                    }

                    continue;
                }
                if (worker.isWorkerShiftActive ||
                    worker.WarehouseWorkerStatus != WarehouseWorkerStatusId.OffShift)
                {
                    throw new InvalidOperationException(
                        $"Unpaid warehouse worker {worker.EntityId} has an active shift state.");
                }

                EconomyDebitEvaluation debit = _solvency.EvaluateDebit(
                    store.EntityId,
                    _config.DailyWage);
                if (!debit.CanDebit)
                    continue;

                int moneyAfterPayment = checked(store.Money - _config.DailyWage);
                int payrollExpensesAfterPayment = checked(
                    store.DayPayrollExpenses + _config.DailyWage);
                long expectedMoney =
                    (long)store.DayOpeningBalance + store.DayRevenue -
                    store.DayProcurementExpenses - store.DayUpgradeExpenses -
                    payrollExpensesAfterPayment;
                if (moneyAfterPayment != debit.MoneyAfterDebit ||
                    expectedMoney != moneyAfterPayment)
                {
                    throw new InvalidOperationException(
                        $"Worker wage would invalidate store {store.EntityId} ledger.");
                }

                store.ReplaceMoney(moneyAfterPayment);
                store.ReplaceDayPayrollExpenses(payrollExpensesAfterPayment);
                worker.ReplaceWorkerPaidDayNumber(store.DayNumber);
                worker.isWorkerShiftActive = true;
                worker.ReplaceWarehouseWorkerStatus(WarehouseWorkerStatusId.Idle);
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationWarehouseWorkerShiftPaid,
                    _config.DailyWage));
            }
        }

        private GameEntity ValidateInteraction(GameEntity player, GameEntity terminal)
        {
            if (!terminal.hasEntityId || !terminal.hasStoreEntityId ||
                !terminal.isInteractable)
            {
                throw new InvalidOperationException(
                    "Store control terminal has incomplete warehouse-worker configuration.");
            }
            if (player == null || !player.isPlayer || !player.hasEntityId ||
                !player.hasStoreEntityId || player.StoreEntityId != terminal.StoreEntityId)
            {
                throw new InvalidOperationException(
                    $"Interaction source cannot pay at terminal {terminal.EntityId}.");
            }

            GameEntity store = _gameContext.GetEntityWithEntityId(terminal.StoreEntityId);
            if (store == null || !store.isStore || !store.hasEntityId ||
                !store.hasStoreControlTerminalEntityId ||
                store.StoreControlTerminalEntityId != terminal.EntityId ||
                !store.hasMoney || !store.hasDayNumber ||
                !store.hasDayOpeningBalance || !store.hasDayRevenue ||
                !store.hasDayProcurementExpenses || !store.hasDayUpgradeExpenses ||
                !store.hasDayPayrollExpenses)
            {
                throw new InvalidOperationException(
                    $"Store control terminal {terminal.EntityId} has an invalid store relation.");
            }
            long expectedMoney =
                (long)store.DayOpeningBalance + store.DayRevenue -
                store.DayProcurementExpenses - store.DayUpgradeExpenses -
                store.DayPayrollExpenses;
            if (store.DayNumber <= 0 || store.Money < 0 ||
                store.DayOpeningBalance < 0 || store.DayRevenue < 0 ||
                store.DayProcurementExpenses < 0 || store.DayUpgradeExpenses < 0 ||
                store.DayPayrollExpenses < 0 || expectedMoney != store.Money)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid ledger before paying wages.");
            }

            return store;
        }

        private static void ValidateWorker(GameEntity worker, GameEntity store)
        {
            if (!worker.isWarehouseWorker || worker.isDestructed ||
                !worker.hasEntityId || !worker.hasWarehouseWorkerStoreEntityId ||
                worker.WarehouseWorkerStoreEntityId != store.EntityId ||
                !worker.hasWorkerPaidDayNumber || !worker.hasWarehouseWorkerStatus ||
                worker.WorkerPaidDayNumber <= 0 ||
                worker.WorkerPaidDayNumber > store.DayNumber)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid unpaid warehouse worker.");
            }
        }
    }
}
