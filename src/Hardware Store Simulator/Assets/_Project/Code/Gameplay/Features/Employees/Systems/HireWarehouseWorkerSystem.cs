using System;
using Entitas;
using HardwareStore.Gameplay.Common.Economy;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.Scene;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class HireWarehouseWorkerSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IStoreSceneData _sceneData;
        private readonly WarehouseWorkerConfig _config;
        private readonly IWarehouseWorkerFactory _workers;
        private readonly IEconomySolvencyService _solvency;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public HireWarehouseWorkerSystem(GameContext gameContext,
            IStoreSceneData sceneData, IStaticDataService staticData,
            IWarehouseWorkerFactory workers, IEconomySolvencyService solvency,
            IGameEventFactory events)
        {
            _gameContext = gameContext;
            _sceneData = sceneData;
            _config = staticData.WarehouseWorker;
            _workers = workers;
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
                        $"Warehouse worker hire targets missing entity " +
                        $"{request.TargetEntityId}.");
                if (!terminal.isStoreControlTerminal)
                    continue;

                GameEntity player = _gameContext.GetEntityWithEntityId(
                    request.SourceEntityId);
                GameEntity store = ValidateInteraction(player, terminal);
                if (!store.isStoreOpen || player.isModalOpen || player.isHandsOccupied ||
                    !store.isWarehouseWorkerHiringUnlocked ||
                    _gameContext.GetEntityWithWarehouseWorkerStoreEntityId(
                        store.EntityId) != null)
                {
                    continue;
                }

                EconomyDebitEvaluation debit = _solvency.EvaluateDebit(
                    store.EntityId,
                    _config.HirePrice);
                if (!debit.CanDebit)
                    continue;

                int moneyAfterHire = checked(store.Money - _config.HirePrice);
                int upgradeExpensesAfterHire = checked(
                    store.DayUpgradeExpenses + _config.HirePrice);
                ValidateDebitResult(
                    store,
                    debit,
                    moneyAfterHire,
                    upgradeExpensesAfterHire,
                    store.DayPayrollExpenses);

                GameEntity worker = _workers.Create(
                    store.EntityId,
                    _sceneData.GetSpawnPoint(SpawnPointId.WarehouseWorker),
                    _sceneData.GetSpawnPoint(SpawnPointId.WarehouseWorkerDeliveryAccess),
                    _sceneData.GetSpawnPoint(SpawnPointId.WarehouseWorkerStorageAccess));
                if (worker == null || !worker.isWarehouseWorker ||
                    !worker.hasEntityId || !worker.hasWarehouseWorkerStoreEntityId ||
                    worker.WarehouseWorkerStoreEntityId != store.EntityId ||
                    !worker.hasWarehouseWorkerStatus || worker.isWorkerShiftActive ||
                    worker.hasWorkerPaidDayNumber)
                {
                    throw new InvalidOperationException(
                        "Warehouse worker factory returned invalid initial state.");
                }

                store.ReplaceMoney(moneyAfterHire);
                store.ReplaceDayUpgradeExpenses(upgradeExpensesAfterHire);
                worker.AddWorkerPaidDayNumber(store.DayNumber);
                worker.isWorkerShiftActive = true;
                worker.ReplaceWarehouseWorkerStatus(WarehouseWorkerStatusId.Idle);
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationWarehouseWorkerHired,
                    _config.HirePrice,
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
                    $"Interaction source cannot hire at terminal {terminal.EntityId}.");
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
                    $"Store {store.EntityId} has an invalid ledger before hiring.");
            }

            return store;
        }

        private static void ValidateDebitResult(GameEntity store,
            EconomyDebitEvaluation debit, int moneyAfterDebit,
            int upgradeExpensesAfterDebit, int payrollExpensesAfterDebit)
        {
            long expectedMoney =
                (long)store.DayOpeningBalance + store.DayRevenue -
                store.DayProcurementExpenses - upgradeExpensesAfterDebit -
                payrollExpensesAfterDebit;
            if (moneyAfterDebit != debit.MoneyAfterDebit ||
                expectedMoney != moneyAfterDebit)
            {
                throw new InvalidOperationException(
                    $"Warehouse worker debit would invalidate store {store.EntityId} ledger.");
            }
        }
    }
}
