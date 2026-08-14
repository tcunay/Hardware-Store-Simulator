using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Cursor;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.StoreDay.Systems
{
    public sealed class StartNextDaySystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly int _startMinute;
        private readonly int _closingMinute;
        private readonly ICursorService _cursor;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;
        private readonly List<GameEntity> _playerBuffer = new(1);

        public StartNextDaySystem(GameContext gameContext, InputContext inputContext,
            IStaticDataService staticData, ICursorService cursor)
        {
            _gameContext = gameContext;
            _startMinute = staticData.StoreDay.StartMinute;
            _closingMinute = staticData.StoreDay.ClosingMinute;
            _cursor = cursor;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.EntityId,
                GameMatcher.MoveDirection,
                GameMatcher.ModalOpen,
                GameMatcher.DayReportStoreEntityId));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(
                InputMatcher.InputState,
                InputMatcher.ConfirmPressed));
        }

        public void Execute()
        {
            foreach (InputEntity ignored in _inputs)
            foreach (GameEntity player in _players.GetEntities(_playerBuffer))
                StartNextDay(player);
        }

        private void StartNextDay(GameEntity player)
        {
            GameEntity store =
                _gameContext.GetEntityWithEntityId(player.DayReportStoreEntityId);
            if (store == null || !store.isStore || !store.isDayReportOpen ||
                store.isStorePreparing || store.isStoreOpen || store.isStoreClosing ||
                !store.hasDayNumber || !store.hasCurrentDayMinute || !store.hasMoney ||
                !store.hasDayOpeningBalance || !store.hasDayRevenue ||
                !store.hasDayProcurementExpenses || !store.hasDayUpgradeExpenses ||
                !store.hasDayPayrollExpenses ||
                !store.hasDayCompletedOrderCount || !store.hasDayLostCustomerCount ||
                !player.hasStoreEntityId || player.StoreEntityId != store.EntityId ||
                player.isHandsOccupied || player.hasConsultationVisitEntityId ||
                player.hasProcurementTerminalEntityId)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} has an invalid day report relation.");
            }
            if (store.CurrentDayMinute != _closingMinute ||
                store.hasCustomerCooldownRemaining ||
                StoreDayCustomerVisitGuard.CountActiveVisits(
                    _gameContext,
                    store.EntityId) != 0)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} cannot advance an unfinished day report.");
            }
            long expectedMoney =
                (long)store.DayOpeningBalance + store.DayRevenue -
                store.DayProcurementExpenses - store.DayUpgradeExpenses -
                store.DayPayrollExpenses;
            if (store.DayNumber <= 0 || store.Money < 0 ||
                store.DayOpeningBalance < 0 || store.DayRevenue < 0 ||
                store.DayProcurementExpenses < 0 || store.DayUpgradeExpenses < 0 ||
                store.DayPayrollExpenses < 0 ||
                store.DayCompletedOrderCount < 0 ||
                store.DayLostCustomerCount < 0 || expectedMoney != store.Money)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} cannot advance an invalid day ledger.");
            }

            int nextDayNumber = checked(store.DayNumber + 1);
            int openingBalance = store.Money;

            store.ReplaceDayNumber(nextDayNumber);
            store.ReplaceCurrentDayMinute(_startMinute);
            store.ReplaceDayOpeningBalance(openingBalance);
            store.ReplaceDayRevenue(0);
            store.ReplaceDayProcurementExpenses(0);
            store.ReplaceDayUpgradeExpenses(0);
            store.ReplaceDayPayrollExpenses(0);
            store.ReplaceDayCompletedOrderCount(0);
            store.ReplaceDayLostCustomerCount(0);
            store.isDayReportOpen = false;
            store.isStorePreparing = true;

            player.RemoveDayReportStoreEntityId();
            player.isModalOpen = false;
            player.ReplaceMoveDirection(Vector3.zero);
            if (player.hasInteractionPrompt)
                player.RemoveInteractionPrompt();
            player.isFocusInteractionAvailable = false;
            if (player.hasFocusedInteractionType)
                player.RemoveFocusedInteractionType();
            if (player.hasFocusedEntityId)
                player.RemoveFocusedEntityId();
            player.isCursorLocked = true;
            _cursor.SetLocked(true);
        }
    }
}
