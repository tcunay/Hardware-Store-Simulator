using System;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveProcurementTerminalPromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;

        public ResolveProcurementTerminalPromptSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.StoreEntityId,
                GameMatcher.FocusedEntityId,
                GameMatcher.FocusedInteractionType));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                if (player.FocusedInteractionType != InteractionTypeId.ProcurementTerminal)
                    continue;

                GameEntity terminal =
                    _gameContext.GetEntityWithEntityId(player.FocusedEntityId);
                if (terminal.StoreEntityId != player.StoreEntityId)
                    continue;
                if (!terminal.hasSelectedProductType)
                    throw new InvalidOperationException(
                        $"Procurement terminal {terminal.EntityId} has no selected product type.");

                if (player.isHandsOccupied)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            player.isPushingTrolley
                                ? LocalizationKey.PromptReleaseTrolleyFirst
                                : LocalizationKey.PromptFreeHandsForProcurement),
                        false);
                    continue;
                }

                GameEntity delivery =
                    _gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                        terminal.EntityId);
                if (delivery != null)
                {
                    int incompleteLineCount = CountIncompleteDeliveryLines(
                        delivery,
                        terminal.EntityId);
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            LocalizationKey.PromptMixedDeliveryBeingStocked,
                            delivery.StockedProductCount,
                            delivery.DeliveryProductCount,
                            incompleteLineCount),
                        false);
                    continue;
                }

                player.SetInteractionPrompt(
                    LocalizedTexts.Text(LocalizationKey.PromptOpenProcurement),
                    true);
            }
        }

        private int CountIncompleteDeliveryLines(GameEntity delivery,
            int terminalEntityId)
        {
            if (!delivery.isDelivery || !delivery.isDeliveryActive ||
                delivery.isDestructed || !delivery.hasEntityId ||
                !delivery.hasDeliveryPurchaseOrderEntityId ||
                !delivery.hasDeliveryProcurementTerminalEntityId ||
                delivery.DeliveryProcurementTerminalEntityId != terminalEntityId ||
                !delivery.hasDeliveryProductCount ||
                !delivery.hasStockedProductCount ||
                delivery.DeliveryProductCount <= 0 ||
                delivery.StockedProductCount < 0 ||
                delivery.StockedProductCount > delivery.DeliveryProductCount)
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminalEntityId} owns an invalid delivery.");
            }

            GameEntity purchaseOrder = _gameContext.GetEntityWithEntityId(
                delivery.DeliveryPurchaseOrderEntityId);
            if (purchaseOrder == null || !purchaseOrder.isPurchaseOrder ||
                purchaseOrder.isDestructed || !purchaseOrder.hasEntityId)
            {
                throw new InvalidOperationException(
                    $"Delivery {delivery.EntityId} references an invalid purchase order.");
            }

            int lineCount = 0;
            int incompleteLineCount = 0;
            int productCount = 0;
            int stockedProductCount = 0;
            foreach (GameEntity line in
                     _gameContext.GetEntitiesWithPurchaseOrderEntityId(
                         purchaseOrder.EntityId))
            {
                if (line.isDestructed)
                    continue;
                if (!line.isPurchaseOrderLine ||
                    !line.hasPurchaseOrderLineProductCount ||
                    !line.hasPurchaseOrderLineStockedProductCount ||
                    line.PurchaseOrderLineProductCount <= 0 ||
                    line.PurchaseOrderLineStockedProductCount < 0 ||
                    line.PurchaseOrderLineStockedProductCount >
                    line.PurchaseOrderLineProductCount)
                {
                    throw new InvalidOperationException(
                        $"Purchase order {purchaseOrder.EntityId} contains an invalid line.");
                }

                lineCount++;
                productCount = checked(
                    productCount + line.PurchaseOrderLineProductCount);
                stockedProductCount = checked(
                    stockedProductCount +
                    line.PurchaseOrderLineStockedProductCount);
                if (line.PurchaseOrderLineStockedProductCount <
                    line.PurchaseOrderLineProductCount)
                {
                    incompleteLineCount++;
                }
            }
            if (lineCount == 0 || productCount != delivery.DeliveryProductCount ||
                stockedProductCount != delivery.StockedProductCount)
            {
                throw new InvalidOperationException(
                    $"Delivery {delivery.EntityId} progress disagrees with its manifest.");
            }

            return incompleteLineCount;
        }
    }
}
