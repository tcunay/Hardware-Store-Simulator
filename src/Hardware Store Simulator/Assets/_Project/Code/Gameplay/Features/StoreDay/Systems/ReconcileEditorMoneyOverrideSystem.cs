#if UNITY_EDITOR
using System.Collections.Generic;
using Entitas;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.StoreDay.Systems
{
    public sealed class ReconcileEditorMoneyOverrideSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _stores;
        private readonly List<GameEntity> _buffer = new(4);

        public ReconcileEditorMoneyOverrideSystem(GameContext gameContext)
        {
            _stores = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Store,
                    GameMatcher.EntityId,
                    GameMatcher.Money,
                    GameMatcher.DayOpeningBalance,
                    GameMatcher.DayRevenue,
                    GameMatcher.DayProcurementExpenses,
                    GameMatcher.DayUpgradeExpenses,
                    GameMatcher.DayPayrollExpenses)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity store in _stores.GetEntities(_buffer))
                Reconcile(store);
        }

        private static void Reconcile(GameEntity store)
        {
            long expectedMoney =
                (long)store.DayOpeningBalance + store.DayRevenue -
                store.DayProcurementExpenses - store.DayUpgradeExpenses -
                store.DayPayrollExpenses;
            if (expectedMoney == store.Money)
                return;

            long balanceDelta = (long)store.Money - expectedMoney;
            long adjustedOpeningBalance = store.DayOpeningBalance + balanceDelta;
            if (store.Money < 0 || adjustedOpeningBalance < 0 ||
                adjustedOpeningBalance > int.MaxValue)
            {
                Debug.LogError(
                    $"[Hardware Store] Cannot reconcile the Editor balance override for " +
                    $"store {store.EntityId}: balance {store.Money:N0}, expected " +
                    $"{expectedMoney:N0}, adjusted opening balance " +
                    $"{adjustedOpeningBalance:N0}. The strict ledger validator will reject " +
                    "this state.");
                return;
            }

            int previousOpeningBalance = store.DayOpeningBalance;
            store.ReplaceDayOpeningBalance((int)adjustedOpeningBalance);
            Debug.LogWarning(
                $"[Hardware Store] Reconciled an Editor balance override for store " +
                $"{store.EntityId}: balance {expectedMoney:N0} -> {store.Money:N0}, " +
                $"day opening balance {previousOpeningBalance:N0} -> " +
                $"{adjustedOpeningBalance:N0}. Player builds remain strictly validated.");
        }
    }
}
#endif
