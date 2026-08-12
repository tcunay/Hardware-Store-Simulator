using System;
using Entitas;
using HardwareStore.Gameplay.Common.Economy;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Trolley.Systems
{
    public sealed class PurchasePlatformTrolleySystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly PlatformTrolleyConfig _config;
        private readonly IPlatformTrolleyFactory _trolleys;
        private readonly IEconomySolvencyService _economySolvency;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public PurchasePlatformTrolleySystem(GameContext gameContext,
            IStaticDataService staticData, IPlatformTrolleyFactory trolleys,
            IGameEventFactory events, IEconomySolvencyService economySolvency)
        {
            _gameContext = gameContext;
            _config = staticData.PlatformTrolley;
            _trolleys = trolleys;
            _events = events;
            _economySolvency = economySolvency;
            _requests = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.InteractionRequest,
                GameMatcher.SourceEntityId,
                GameMatcher.TargetEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity request in _requests)
            {
                GameEntity terminal =
                    _gameContext.GetEntityWithEntityId(request.TargetEntityId);
                if (terminal == null)
                    throw new InvalidOperationException(
                        $"Trolley interaction targets missing entity " +
                        $"{request.TargetEntityId}.");
                if (!terminal.isTrolleyUpgradeTerminal)
                    continue;

                ValidateTerminal(terminal);
                GameEntity player =
                    _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                ValidatePlayer(player, terminal);
                if (player.isModalOpen)
                    continue;
                if (player.isHandsOccupied)
                {
                    _events.EmitNotification(LocalizedTexts.Text(
                        player.isPushingTrolley
                            ? LocalizationKey.NotificationReleaseTrolleyFirst
                            : LocalizationKey.NotificationFreeHandsForTrolleyUpgrade));
                    continue;
                }
                GameEntity store =
                    _gameContext.GetEntityWithEntityId(terminal.StoreEntityId);
                ValidateStore(store, terminal);

                if (_gameContext.GetEntityWithTrolleyStoreEntityId(store.EntityId) != null)
                {
                    _events.EmitNotification(LocalizedTexts.Text(
                        LocalizationKey.NotificationTrolleyAlreadyPurchased));
                    continue;
                }

                if (!store.isTrolleyUpgradeUnlocked)
                {
                    _events.EmitNotification(LocalizedTexts.Text(
                        LocalizationKey.NotificationTrolleyUpgradeLocked,
                        store.CompletedOrderCount,
                        _config.RequiredCompletedOrderCount));
                    continue;
                }

                if (store.Money < _config.PurchasePrice)
                {
                    _events.EmitNotification(LocalizedTexts.Text(
                        LocalizationKey.NotificationTrolleyInsufficientMoney,
                        _config.PurchasePrice));
                    continue;
                }

                EconomyDebitEvaluation debit = _economySolvency.EvaluateDebit(
                    store.EntityId,
                    _config.PurchasePrice);
                if (debit.Availability ==
                    EconomyDebitAvailability.DemandWouldBecomeInsolvent)
                {
                    _events.EmitNotification(LocalizedTexts.Text(
                        LocalizationKey.NotificationTrolleyPurchaseWouldBlockProjects));
                    continue;
                }
                if (!debit.CanDebit)
                {
                    throw new InvalidOperationException(
                        $"Trolley debit evaluation for store {store.EntityId} disagrees " +
                        "with the validated money balance.");
                }

                int moneyAfterPurchase = checked(store.Money - _config.PurchasePrice);
                int upgradeExpensesAfterPurchase = checked(
                    store.DayUpgradeExpenses + _config.PurchasePrice);
                long ledgerBalanceAfterPurchase =
                    (long)store.DayOpeningBalance + store.DayRevenue -
                    store.DayProcurementExpenses - upgradeExpensesAfterPurchase -
                    store.DayPayrollExpenses;
                if (moneyAfterPurchase != debit.MoneyAfterDebit ||
                    ledgerBalanceAfterPurchase != moneyAfterPurchase)
                {
                    throw new InvalidOperationException(
                        $"Trolley purchase would invalidate store {store.EntityId} day ledger.");
                }

                var spawnPose = new Pose(
                    terminal.TrolleySpawnPosition,
                    terminal.TrolleySpawnRotation);
                _trolleys.Create(spawnPose, store.EntityId);
                store.ReplaceMoney(moneyAfterPurchase);
                store.ReplaceDayUpgradeExpenses(upgradeExpensesAfterPurchase);
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationTrolleyPurchased,
                    _config.PurchasePrice));
                _events.EmitAudio(HardwareStore.Gameplay.Components.AudioCueId.DeliveryPurchased);
            }
        }

        private static void ValidateTerminal(GameEntity terminal)
        {
            if (!terminal.hasEntityId || !terminal.hasStoreEntityId ||
                !terminal.hasTrolleySpawnPosition ||
                !terminal.hasTrolleySpawnRotation || !terminal.isInteractable)
            {
                throw new InvalidOperationException(
                    "Platform trolley upgrade terminal has incomplete configuration.");
            }
        }

        private static void ValidatePlayer(GameEntity player, GameEntity terminal)
        {
            if (player == null || !player.isPlayer || !player.hasEntityId ||
                !player.hasStoreEntityId || player.StoreEntityId != terminal.StoreEntityId)
            {
                throw new InvalidOperationException(
                    $"Trolley purchase source cannot use terminal {terminal.EntityId}.");
            }
        }

        private static void ValidateStore(GameEntity store, GameEntity terminal)
        {
            if (store == null || !store.isStore || !store.hasEntityId ||
                !store.hasMoney || !store.hasCompletedOrderCount ||
                !store.hasDayOpeningBalance || !store.hasDayRevenue ||
                !store.hasDayProcurementExpenses || !store.hasDayUpgradeExpenses ||
                !store.hasDayPayrollExpenses ||
                !store.hasTrolleyUpgradeTerminalEntityId ||
                store.TrolleyUpgradeTerminalEntityId != terminal.EntityId)
            {
                throw new InvalidOperationException(
                    $"Trolley terminal {terminal.EntityId} has an invalid store relation.");
            }
            if (store.Money < 0 || store.DayOpeningBalance < 0 ||
                store.DayRevenue < 0 || store.DayProcurementExpenses < 0 ||
                store.DayUpgradeExpenses < 0 || store.DayPayrollExpenses < 0 ||
                (long)store.DayOpeningBalance + store.DayRevenue -
                store.DayProcurementExpenses - store.DayUpgradeExpenses -
                store.DayPayrollExpenses != store.Money)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} day ledger is inconsistent before trolley purchase.");
            }
        }
    }
}
