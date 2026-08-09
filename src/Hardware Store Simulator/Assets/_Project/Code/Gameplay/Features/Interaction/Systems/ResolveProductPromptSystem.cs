using System;
using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Components;

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

                GameEntity product = _gameContext.GetRequiredEntity(
                    player.FocusedEntityId,
                    "focused product");
                if (!product.isProduct)
                    throw new InvalidOperationException(
                        $"Entity {product.EntityId} is not a product.");
                if (product.isInboundProduct && product.isInStock)
                    throw new InvalidOperationException(
                        $"Product {product.EntityId} cannot be inbound and in stock at the same time.");

                GameEntity store = _gameContext.RequireStore(player);
                GameEntity inboundDelivery = null;
                if (product.isInboundProduct)
                {
                    if (!product.hasDeliveryEntityId)
                        throw new InvalidOperationException(
                            $"Inbound product {product.EntityId} has no delivery relation.");

                    inboundDelivery = _gameContext.GetRequiredEntity(
                        product.DeliveryEntityId,
                        "inbound product delivery");
                    if (!inboundDelivery.isDelivery ||
                        !inboundDelivery.hasProcurementTerminalEntityId)
                        throw new InvalidOperationException(
                            $"Inbound product {product.EntityId} references invalid delivery " +
                            $"{product.DeliveryEntityId}.");
                    if (inboundDelivery.ProcurementTerminalEntityId !=
                        store.ProcurementTerminalEntityId)
                        continue;
                }
                else if (product.isInStock)
                {
                    if (!product.hasStorageZoneEntityId)
                        throw new InvalidOperationException(
                            $"In-stock product {product.EntityId} has no storage ownership relation.");
                    if (product.StorageZoneEntityId != store.StorageZoneEntityId)
                        continue;
                }

                if (product.isLoaded)
                {
                    player.SetInteractionPrompt("Товар уже загружен клиенту", false);
                    continue;
                }

                if (player.hasHeldProductId)
                {
                    player.SetInteractionPrompt(
                        "Руки заняты — G, чтобы бросить мешок",
                        false);
                    continue;
                }

                if (product.isInboundProduct)
                {
                    if (!inboundDelivery.isDeliveryActive)
                        throw new InvalidOperationException(
                            $"Inbound product {product.EntityId} is not linked to an active delivery.");

                    player.SetInteractionPrompt(
                        "E — взять мешок из поставки",
                        true);
                    continue;
                }

                if (!product.isInStock)
                {
                    player.SetInteractionPrompt(
                        "Этот товар сейчас нельзя взять",
                        false);
                    continue;
                }

                GameEntity order = _gameContext.GetRequiredEntity(
                    store.OrderEntityId,
                    "store order");
                if (!order.isOrder ||
                    order.StoreEntityId != store.EntityId ||
                    order.StorageZoneEntityId != store.StorageZoneEntityId)
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} has an invalid order relation.");

                if (order.isOrderWaiting)
                {
                    player.SetInteractionPrompt(
                        "Сначала примите заказ у стойки",
                        false);
                    continue;
                }

                if (order.isOrderCompleted)
                {
                    player.SetInteractionPrompt("Заказ уже выполнен", false);
                    continue;
                }

                if (!order.isOrderActive)
                    throw new InvalidOperationException(
                        $"Order {order.EntityId} has no valid lifecycle state.");

                bool available = product.ProductType == order.RequiredProductType;
                player.SetInteractionPrompt(
                    available
                        ? "E — взять мешок со склада"
                        : "Для активного заказа нужен другой товар",
                    available);
            }
        }
    }
}
