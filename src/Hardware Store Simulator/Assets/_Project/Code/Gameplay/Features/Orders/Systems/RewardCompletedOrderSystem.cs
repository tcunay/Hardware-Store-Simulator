using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Orders.Systems
{
    public sealed class RewardCompletedOrderSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public RewardCompletedOrderSystem(GameContext gameContext, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.Order,
                    GameMatcher.CustomerVisitCompleted,
                    GameMatcher.CustomerVisitStoreEntityId,
                    GameMatcher.OrderReward)
                .NoneOf(
                    GameMatcher.OrderRewarded,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
            {
                GameEntity store =
                    _gameContext.GetEntityWithEntityId(visit.CustomerVisitStoreEntityId);

                ValidateStoreLedger(store, visit);
                int moneyAfterReward = checked(store.Money + visit.OrderReward);
                int revenueAfterReward = checked(store.DayRevenue + visit.OrderReward);
                int completedOrdersAfterReward =
                    checked(store.DayCompletedOrderCount + 1);
                long ledgerBalanceAfterReward =
                    (long)store.DayOpeningBalance + revenueAfterReward -
                    store.DayProcurementExpenses - store.DayUpgradeExpenses -
                    store.DayPayrollExpenses;
                if (ledgerBalanceAfterReward != moneyAfterReward)
                {
                    throw new InvalidOperationException(
                        $"Reward for visit {visit.EntityId} would invalidate store " +
                        $"{store.EntityId} day ledger.");
                }

                store.ReplaceMoney(moneyAfterReward);
                store.ReplaceDayRevenue(revenueAfterReward);
                store.ReplaceDayCompletedOrderCount(completedOrdersAfterReward);
                visit.isOrderRewarded = true;
                _events.EmitAudio(AudioCueId.Reward);
            }
        }

        private static void ValidateStoreLedger(GameEntity store, GameEntity visit)
        {
            if (store == null || !store.isStore || !store.hasEntityId ||
                !store.hasMoney || !store.hasDayOpeningBalance ||
                !store.hasDayRevenue || !store.hasDayProcurementExpenses ||
                !store.hasDayUpgradeExpenses || !store.hasDayPayrollExpenses ||
                !store.hasDayCompletedOrderCount ||
                store.Money < 0 || store.DayOpeningBalance < 0 ||
                store.DayRevenue < 0 || store.DayProcurementExpenses < 0 ||
                store.DayUpgradeExpenses < 0 || store.DayPayrollExpenses < 0 ||
                store.DayCompletedOrderCount < 0)
            {
                throw new InvalidOperationException(
                    $"Completed visit {visit.EntityId} references an invalid store ledger.");
            }

            long expectedMoney =
                (long)store.DayOpeningBalance + store.DayRevenue -
                store.DayProcurementExpenses - store.DayUpgradeExpenses -
                store.DayPayrollExpenses;
            if (expectedMoney != store.Money)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} day ledger is inconsistent before reward.");
            }
        }
    }
}
