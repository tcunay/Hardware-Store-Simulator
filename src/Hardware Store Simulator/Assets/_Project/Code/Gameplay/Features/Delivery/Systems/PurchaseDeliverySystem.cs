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
        private readonly IPurchaseOrderFactory _purchaseOrders;
        private readonly IDeliveryFactory _deliveryFactory;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public PurchaseDeliverySystem(GameContext gameContext,
            IProcurementSolvencyService solvency,
            IPurchaseOrderFactory purchaseOrders,
            IDeliveryFactory deliveryFactory, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _solvency = solvency;
            _purchaseOrders = purchaseOrders;
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
                if (_gameContext.GetEntityWithPurchaseOrderProcurementTerminalEntityId(
                        terminal.EntityId) != null)
                {
                    throw new InvalidOperationException(
                        $"Procurement terminal {terminal.EntityId} has an orphaned " +
                        "purchase order.");
                }

                GameEntity store =
                    _gameContext.GetEntityWithEntityId(terminal.StoreEntityId);
                GameEntity cart =
                    _gameContext.GetEntityWithProcurementCartTerminalEntityId(
                        terminal.EntityId);
                if (cart == null)
                {
                    throw new InvalidOperationException(
                        $"Procurement terminal {terminal.EntityId} has no cart.");
                }
                bool hasCartLines = ValidateCartAndHasLines(cart, terminal);
                if (!hasCartLines)
                    continue;
                ProcurementPurchaseEvaluation evaluation =
                    _solvency.EvaluateCart(cart.EntityId);
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
                    LocalizationKey blockedNotification = evaluation.DemandKind switch
                    {
                        ProcurementDemandKind.ConfirmedOrder =>
                            LocalizationKey.NotificationPurchaseWouldBlockOrder,
                        ProcurementDemandKind.SelectedCustomerOrder =>
                            LocalizationKey.NotificationPurchaseWouldBlockOrder,
                        ProcurementDemandKind.ProjectForecast =>
                            LocalizationKey.NotificationPurchaseWouldBlockForecast,
                        _ => throw new ArgumentOutOfRangeException(
                            nameof(evaluation.DemandKind), evaluation.DemandKind, null)
                    };
                    _events.EmitNotification(LocalizedTexts.Text(
                        blockedNotification));
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
                    procurementExpensesAfterPurchase - store.DayUpgradeExpenses -
                    store.DayPayrollExpenses;
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
                GameEntity order = _purchaseOrders.Create(
                    cart.EntityId,
                    terminal.EntityId,
                    store.EntityId);
                if (order.PurchaseOrderProductCount != evaluation.DeliveryProductCount ||
                    order.PurchaseOrderCost != evaluation.DeliveryCost)
                {
                    throw new InvalidOperationException(
                        $"Purchase order {order.EntityId} disagrees with its solvency " +
                        "evaluation.");
                }

                GameEntity delivery = _deliveryFactory.Create(
                    order.EntityId,
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
                    LocalizationKey.NotificationMixedDeliveryOrdered,
                    order.PurchaseOrderPackageCount,
                    order.PurchaseOrderProductCount,
                    order.PurchaseOrderCost));
                _events.EmitAudio(AudioCueId.DeliveryPurchased);
                ClearCart(cart);
                request.isPurchaseDeliverySucceeded = true;
            }
        }

        private bool ValidateCartAndHasLines(GameEntity cart, GameEntity terminal)
        {
            if (!cart.isProcurementCart || cart.isDestructed || !cart.hasEntityId ||
                !cart.hasProcurementCartTerminalEntityId ||
                cart.ProcurementCartTerminalEntityId != terminal.EntityId ||
                !cart.hasStoreEntityId || cart.StoreEntityId != terminal.StoreEntityId ||
                !cart.hasProcurementCartPackageCapacity ||
                cart.ProcurementCartPackageCapacity <= 0 ||
                cart.ProcurementCartPackageCapacity >
                ProcurementCartFactory.CurrentDeliveryPackageCapacity)
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminal.EntityId} owns an invalid cart.");
            }

            bool hasLines = false;
            foreach (GameEntity line in _gameContext.GetEntitiesWithProcurementCartEntityId(
                         cart.EntityId))
            {
                if (line.isDestructed)
                    continue;
                if (!line.isProcurementCartLine || !line.hasEntityId ||
                    !line.hasProductType || !line.hasProcurementPackageCount ||
                    line.ProcurementPackageCount <= 0)
                {
                    throw new InvalidOperationException(
                        $"Procurement cart {cart.EntityId} contains an invalid line.");
                }
                hasLines = true;
            }
            return hasLines;
        }

        private void ClearCart(GameEntity cart)
        {
            foreach (GameEntity line in _gameContext.GetEntitiesWithProcurementCartEntityId(
                         cart.EntityId))
            {
                if (!line.isDestructed)
                    line.isDestructed = true;
            }
        }

        private static void ValidateStoreLedger(GameEntity store, GameEntity terminal)
        {
            if (store == null || !store.isStore || !store.hasEntityId ||
                !store.hasMoney || !store.hasDayOpeningBalance ||
                !store.hasDayRevenue || !store.hasDayProcurementExpenses ||
                !store.hasDayUpgradeExpenses || !store.hasDayPayrollExpenses ||
                store.Money < 0 ||
                store.DayOpeningBalance < 0 || store.DayRevenue < 0 ||
                store.DayProcurementExpenses < 0 || store.DayUpgradeExpenses < 0 ||
                store.DayPayrollExpenses < 0)
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminal.EntityId} references an invalid " +
                    "store ledger.");
            }

            long expectedMoney =
                (long)store.DayOpeningBalance + store.DayRevenue -
                store.DayProcurementExpenses - store.DayUpgradeExpenses -
                store.DayPayrollExpenses;
            if (expectedMoney != store.Money)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} day ledger is inconsistent before purchase.");
            }
        }

    }
}
