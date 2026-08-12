using System;
using Entitas;
using HardwareStore.Gameplay.Common.Economy;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Delivery.Systems
{
    public sealed class PurchaseDeliverySystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IProcurementSolvencyService _solvency;
        private readonly IDeliveryFactory _deliveryFactory;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public PurchaseDeliverySystem(GameContext gameContext,
            IProcurementSolvencyService solvency,
            IDeliveryFactory deliveryFactory, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _solvency = solvency;
            _deliveryFactory = deliveryFactory;
            _events = events;
            _requests = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.PurchaseDeliveryRequest,
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
                        $"Purchase request targets missing entity {request.TargetEntityId}.");
                if (!terminal.isProcurementTerminal)
                    throw new InvalidOperationException(
                        $"Purchase request targets non-procurement entity " +
                        $"{terminal.EntityId}.");
                if (!terminal.hasSelectedProductType)
                    throw new InvalidOperationException(
                        $"Procurement terminal {terminal.EntityId} has no selected product type.");

                GameEntity player =
                    _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                if (player == null || !player.isPlayer || !player.isModalOpen ||
                    !player.hasEntityId || !player.hasStoreEntityId ||
                    !player.hasProcurementTerminalEntityId ||
                    player.ProcurementTerminalEntityId != terminal.EntityId ||
                    player.StoreEntityId != terminal.StoreEntityId)
                {
                    throw new InvalidOperationException(
                        $"Purchase request source {request.SourceEntityId} does not own " +
                        "procurement " +
                        $"modal for terminal {terminal.EntityId}.");
                }

                if (_gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                        terminal.EntityId) != null)
                {
                    _events.EmitNotification(LocalizedTexts.Text(
                        LocalizationKey.NotificationAcceptCurrentDeliveryFirst));
                    continue;
                }

                GameEntity store =
                    _gameContext.GetEntityWithEntityId(terminal.StoreEntityId);
                ProcurementPurchaseEvaluation evaluation = _solvency.EvaluatePurchase(
                    terminal.EntityId,
                    terminal.SelectedProductType);
                if (evaluation.Availability ==
                    ProcurementPurchaseAvailability.InsufficientStorage)
                {
                    GameEntity storageZone = _gameContext.GetEntityWithEntityId(
                        terminal.StorageZoneEntityId);
                    if (storageZone == null || !storageZone.isStorageZone ||
                        !storageZone.hasSlots ||
                        !storageZone.hasOccupiedStorageSlotCount)
                    {
                        throw new InvalidOperationException(
                            $"Procurement terminal {terminal.EntityId} has invalid storage " +
                            "after solvency evaluation.");
                    }
                    int freeSlotCount = checked(
                        storageZone.Slots.Length -
                        storageZone.OccupiedStorageSlotCount);
                    _events.EmitNotification(LocalizedTexts.Text(
                        LocalizationKey.NotificationStorageSpaceInsufficient,
                        freeSlotCount,
                        evaluation.DeliveryProductCount));
                    continue;
                }

                if (evaluation.Availability ==
                    ProcurementPurchaseAvailability.InsufficientMoney)
                {
                    _events.EmitNotification(LocalizedTexts.Text(
                        LocalizationKey.NotificationMoneyInsufficient,
                        evaluation.DeliveryCost));
                    continue;
                }

                if (evaluation.Availability ==
                    ProcurementPurchaseAvailability.DemandWouldBecomeInsolvent)
                {
                    _events.EmitNotification(LocalizedTexts.Text(
                        evaluation.DemandKind == ProcurementDemandKind.ConfirmedOrder
                            ? LocalizationKey.NotificationPurchaseWouldBlockOrder
                            : LocalizationKey.NotificationPurchaseWouldBlockForecast));
                    continue;
                }
                if (!evaluation.CanPurchase)
                    throw new InvalidOperationException(
                        $"Unhandled procurement evaluation {evaluation.Availability}.");

                ValidateStoreLedger(store, terminal);
                int moneyAfterPurchase = checked(store.Money - evaluation.DeliveryCost);
                int procurementExpensesAfterPurchase = checked(
                    store.DayProcurementExpenses + evaluation.DeliveryCost);
                long ledgerBalanceAfterPurchase =
                    (long)store.DayOpeningBalance + store.DayRevenue -
                    procurementExpensesAfterPurchase - store.DayUpgradeExpenses;
                if (moneyAfterPurchase != evaluation.MoneyAfterPurchase ||
                    ledgerBalanceAfterPurchase != moneyAfterPurchase)
                {
                    throw new InvalidOperationException(
                        $"Purchase evaluation for terminal {terminal.EntityId} would " +
                        $"invalidate store {store.EntityId} day ledger.");
                }

                var deliveryPose = new Pose(
                    terminal.DeliverySpawnPosition,
                    terminal.DeliverySpawnRotation);
                GameEntity delivery = _deliveryFactory.Create(
                    terminal.SelectedProductType,
                    terminal.EntityId,
                    store.EntityId,
                    deliveryPose);

                if (delivery.DeliveryProductCount != evaluation.DeliveryProductCount ||
                    delivery.DeliveryCost != evaluation.DeliveryCost)
                {
                    throw new InvalidOperationException(
                        $"Delivery {delivery.EntityId} disagrees with its evaluated purchase.");
                }

                store.ReplaceMoney(moneyAfterPurchase);
                store.ReplaceDayProcurementExpenses(procurementExpensesAfterPurchase);
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationDeliveryOrdered,
                    LocalizedTexts.ProductName(delivery.ProductType),
                    delivery.DeliveryProductCount,
                    LocalizedTexts.ProductUnit(delivery.ProductType),
                    delivery.DeliveryCost));
                _events.EmitAudio(AudioCueId.DeliveryPurchased);
                request.isPurchaseDeliverySucceeded = true;
            }
        }

        private static void ValidateStoreLedger(GameEntity store, GameEntity terminal)
        {
            if (store == null || !store.isStore || !store.hasEntityId ||
                !store.hasMoney || !store.hasDayOpeningBalance ||
                !store.hasDayRevenue || !store.hasDayProcurementExpenses ||
                !store.hasDayUpgradeExpenses || store.Money < 0 ||
                store.DayOpeningBalance < 0 || store.DayRevenue < 0 ||
                store.DayProcurementExpenses < 0 || store.DayUpgradeExpenses < 0)
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminal.EntityId} references an invalid " +
                    "store ledger.");
            }

            long expectedMoney =
                (long)store.DayOpeningBalance + store.DayRevenue -
                store.DayProcurementExpenses - store.DayUpgradeExpenses;
            if (expectedMoney != store.Money)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} day ledger is inconsistent before purchase.");
            }
        }

    }
}
