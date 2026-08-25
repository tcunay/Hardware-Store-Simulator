using System;
using System.Linq;
using Entitas;
using HardwareStore.Gameplay.Common;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveProductPromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;

        public ResolveProductPromptSystem(GameContext gameContext)
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
                if (player.FocusedInteractionType != InteractionTypeId.Product)
                    continue;

                GameEntity product =
                    _gameContext.GetEntityWithEntityId(player.FocusedEntityId);
                if (product.isInboundProduct)
                    InboundProductManifestValidator.Validate(_gameContext, product);
                LocalizedText productName = LocalizedTexts.ProductName(product.ProductType);
                GameEntity store =
                    _gameContext.GetEntityWithEntityId(player.StoreEntityId);
                if (product.isInboundProduct)
                {
                    GameEntity terminal = _gameContext.GetEntityWithEntityId(
                        store.ProcurementTerminalEntityId);
                    GameEntity delivery =
                        _gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                            terminal.EntityId);
                    if (delivery == null || delivery.EntityId != product.DeliveryEntityId)
                        continue;
                }
                else if (product.isInStock)
                {
                    if (product.StorageZoneEntityId != store.StorageZoneEntityId)
                        continue;
                }

                if (product.isLoaded)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(LocalizationKey.PromptLoadedProduct, productName),
                        false);
                    continue;
                }

                if (player.isHandsOccupied)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            player.isPushingTrolley
                                ? LocalizationKey.PromptReleaseTrolleyFirst
                                : LocalizationKey.PromptHandsOccupied),
                        false);
                    continue;
                }

                if (product.isInboundProduct)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            LocalizationKey.PromptPickInboundProduct,
                            productName),
                        true);
                    continue;
                }

                if (!product.isInStock)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(LocalizationKey.PromptProductUnavailable),
                        false);
                    continue;
                }

                GameEntity customerVisit =
                    _gameContext.GetCurrentLoadingVisit(store.EntityId);
                if (customerVisit == null)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            LocalizationKey.PromptNoCustomerProductNotRequired),
                        false);
                    continue;
                }

                GameEntity[] lines = GetOrderLines(customerVisit);
                GameEntity matchingLine = lines.FirstOrDefault(line =>
                    line.ProductType == product.ProductType);
                bool alreadyReserved = product.hasReservedOrderLineEntityId;
                if (alreadyReserved &&
                    (matchingLine == null ||
                     product.ReservedOrderLineEntityId != matchingLine.EntityId))
                {
                    throw new InvalidOperationException(
                        $"Product {product.EntityId} reserves an order line that does not match " +
                        "the active customer order.");
                }

                int reservedProductCount = matchingLine == null
                    ? 0
                    : CountReservedProducts(matchingLine);
                if (matchingLine != null &&
                    matchingLine.LoadedProductCount + reservedProductCount >
                    matchingLine.RequiredProductCount)
                {
                    throw new InvalidOperationException(
                        $"Order line {matchingLine.EntityId} exceeds its reserved quota.");
                }
                if (alreadyReserved &&
                    matchingLine.LoadedProductCount >= matchingLine.RequiredProductCount)
                {
                    throw new InvalidOperationException(
                        $"Product {product.EntityId} reserves an already loaded order line.");
                }
                bool available = matchingLine != null &&
                                 (alreadyReserved ||
                                  matchingLine.LoadedProductCount + reservedProductCount <
                                  matchingLine.RequiredProductCount);
                player.SetInteractionPrompt(
                    available
                        ? LocalizedTexts.Text(LocalizationKey.PromptPickStockProduct, productName)
                        : matchingLine == null
                            ? LocalizedTexts.Text(
                                LocalizationKey.PromptProductNotInOrder,
                                productName)
                            : LocalizedTexts.Text(
                                LocalizationKey.PromptOrderLineAlreadyLoaded,
                                productName),
                    available);
            }
        }

        private GameEntity[] GetOrderLines(GameEntity order)
        {
            GameEntity[] lines = _gameContext
                .GetEntitiesWithOrderEntityId(order.EntityId)
                .Where(line => line.isOrderLine && !line.isDestructed)
                .OrderBy(line => line.LineIndex)
                .ToArray();
            if (lines.Length == 0)
                throw new InvalidOperationException(
                    $"Order {order.EntityId} has no active product lines.");

            return lines;
        }

        private int CountReservedProducts(GameEntity orderLine)
        {
            int count = 0;
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithReservedOrderLineEntityId(orderLine.EntityId))
            {
                if (!product.isProduct || product.isDestructed || !product.isInStock ||
                    !product.hasEntityId || !product.hasProductType ||
                    !product.hasStorageZoneEntityId ||
                    !product.hasReservedStorageSlotIndex ||
                    product.ReservedOrderLineEntityId != orderLine.EntityId ||
                    product.ProductType != orderLine.ProductType ||
                    product.StorageZoneEntityId != orderLine.StorageZoneEntityId)
                {
                    throw new InvalidOperationException(
                        $"Order line {orderLine.EntityId} has an invalid product reservation.");
                }

                count++;
            }

            return count;
        }
    }
}
