using System;
using Entitas;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.StoreDay.Systems
{
    public sealed class ValidateStoreDayStateSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly int _startMinute;
        private readonly int _closingMinute;
        private readonly IGroup<GameEntity> _stores;
        private readonly IGroup<GameEntity> _reportPlayers;

        public ValidateStoreDayStateSystem(GameContext gameContext,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _startMinute = staticData.StoreDay.StartMinute;
            _closingMinute = staticData.StoreDay.ClosingMinute;
            _stores = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Store,
                    GameMatcher.EntityId,
                    GameMatcher.Money,
                    GameMatcher.DayNumber,
                    GameMatcher.CurrentDayMinute,
                    GameMatcher.DayOpeningBalance,
                    GameMatcher.DayRevenue,
                    GameMatcher.DayProcurementExpenses,
                    GameMatcher.DayUpgradeExpenses,
                    GameMatcher.DayPayrollExpenses,
                    GameMatcher.DayCompletedOrderCount,
                    GameMatcher.StoreControlTerminalEntityId)
                .NoneOf(GameMatcher.Destructed));
            _reportPlayers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Player,
                    GameMatcher.EntityId,
                    GameMatcher.DayReportStoreEntityId)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity store in _stores)
                ValidateStore(store);

            foreach (GameEntity player in _reportPlayers)
                ValidateReportPlayer(player);
        }

        private void ValidateStore(GameEntity store)
        {
            int phaseCount =
                (store.isStorePreparing ? 1 : 0) +
                (store.isStoreOpen ? 1 : 0) +
                (store.isStoreClosing ? 1 : 0) +
                (store.isDayReportOpen ? 1 : 0);
            if (phaseCount != 1)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} must have exactly one day-cycle phase, " +
                    $"but has {phaseCount}.");
            }
            if (store.DayNumber <= 0 || store.Money < 0 ||
                store.DayOpeningBalance < 0 || store.DayRevenue < 0 ||
                store.DayProcurementExpenses < 0 || store.DayUpgradeExpenses < 0 ||
                store.DayPayrollExpenses < 0 ||
                store.DayCompletedOrderCount < 0)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has invalid day-cycle financial values.");
            }

            long expectedMoney =
                (long)store.DayOpeningBalance + store.DayRevenue -
                store.DayProcurementExpenses - store.DayUpgradeExpenses -
                store.DayPayrollExpenses;
            if (expectedMoney != store.Money)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} violates its day ledger: expected balance " +
                    $"{expectedMoney}, actual balance {store.Money}.");
            }

            GameEntity terminal = _gameContext.GetEntityWithEntityId(
                store.StoreControlTerminalEntityId);
            if (terminal == null || terminal.isDestructed ||
                !terminal.isStoreControlTerminal || !terminal.isInteractable ||
                !terminal.hasStoreEntityId || terminal.StoreEntityId != store.EntityId)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid store control terminal relation.");
            }

            int activeVisitCount = StoreDayCustomerVisitGuard.CountActiveVisits(
                _gameContext,
                store.EntityId);
            GameEntity reportPlayer =
                _gameContext.GetEntityWithDayReportStoreEntityId(store.EntityId);

            if (store.isStorePreparing)
            {
                ValidateMinute(store, _startMinute);
                if (store.hasCustomerCooldownRemaining || activeVisitCount != 0 ||
                    reportPlayer != null)
                {
                    throw new InvalidOperationException(
                        $"Preparing store {store.EntityId} has active customer-day state.");
                }
                return;
            }

            if (store.isStoreOpen)
            {
                if (float.IsNaN(store.CurrentDayMinute) ||
                    float.IsInfinity(store.CurrentDayMinute) ||
                    store.CurrentDayMinute < _startMinute ||
                    store.CurrentDayMinute >= _closingMinute)
                {
                    throw new InvalidOperationException(
                        $"Open store {store.EntityId} has invalid time " +
                        $"{store.CurrentDayMinute}.");
                }
                if (!store.hasCustomerCooldownRemaining || reportPlayer != null)
                {
                    throw new InvalidOperationException(
                        $"Open store {store.EntityId} must own its customer arrival cooldown " +
                        "and cannot own a day report.");
                }
                ValidateCustomerCooldown(store);
                return;
            }

            ValidateMinute(store, _closingMinute);
            if (store.hasCustomerCooldownRemaining)
            {
                throw new InvalidOperationException(
                    $"Closed store {store.EntityId} cannot schedule another customer.");
            }

            if (store.isStoreClosing)
            {
                if (reportPlayer != null)
                {
                    throw new InvalidOperationException(
                        $"Closing store {store.EntityId} cannot already own a day report.");
                }
                return;
            }

            if (activeVisitCount != 0 || reportPlayer == null)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} can show its report only after its customer leaves " +
                    "and to exactly one player.");
            }
        }

        private void ValidateReportPlayer(GameEntity player)
        {
            GameEntity store =
                _gameContext.GetEntityWithEntityId(player.DayReportStoreEntityId);
            if (store == null || !store.isStore || !store.isDayReportOpen ||
                !player.isModalOpen || player.isHandsOccupied ||
                !player.hasStoreEntityId || player.StoreEntityId != store.EntityId)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} has an invalid day report relation.");
            }
        }

        private static void ValidateMinute(GameEntity store, int expectedMinute)
        {
            if (store.CurrentDayMinute != expectedMinute)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} expects minute {expectedMinute}, but has " +
                    $"{store.CurrentDayMinute}.");
            }
        }

        private static void ValidateCustomerCooldown(GameEntity store)
        {
            float cooldown = store.CustomerCooldownRemaining;
            if (float.IsNaN(cooldown) || float.IsInfinity(cooldown) || cooldown < 0f)
            {
                throw new InvalidOperationException(
                    $"Open store {store.EntityId} has invalid customer cooldown {cooldown}.");
            }
        }
    }

    internal static class StoreDayCustomerVisitGuard
    {
        public static int CountActiveVisits(
            GameContext gameContext,
            int storeEntityId)
        {
            int count = 0;
            foreach (GameEntity visit in
                     gameContext.GetEntitiesWithCustomerVisitStoreEntityId(storeEntityId))
            {
                ValidateVisit(visit, storeEntityId);
                count++;
            }

            return count;
        }

        private static void ValidateVisit(GameEntity visit, int storeEntityId)
        {
            if (visit == null || visit.isDestructed || !visit.isCustomerVisit ||
                !visit.isCustomerVehicle || !visit.hasEntityId ||
                !visit.hasCustomerVisitStoreEntityId ||
                visit.CustomerVisitStoreEntityId != storeEntityId)
            {
                throw new InvalidOperationException(
                    $"Store {storeEntityId} has an invalid indexed customer visit.");
            }

            int lifecycleCount =
                (visit.isCustomerVisitArriving ? 1 : 0) +
                (visit.isCustomerVisitQueued ? 1 : 0) +
                (visit.isCustomerVisitConsulting ? 1 : 0) +
                (visit.isCustomerVisitWaitingForLoadingBay ? 1 : 0) +
                (visit.isCustomerVisitMovingToLoadingBay ? 1 : 0) +
                (visit.isCustomerVisitLoading ? 1 : 0) +
                (visit.isCustomerVisitCompleted ? 1 : 0) +
                (visit.isCustomerVisitReturning ? 1 : 0) +
                (visit.isCustomerVisitDeparting ? 1 : 0);
            if (lifecycleCount != 1)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} must have exactly one lifecycle marker.");
            }
        }
    }
}
