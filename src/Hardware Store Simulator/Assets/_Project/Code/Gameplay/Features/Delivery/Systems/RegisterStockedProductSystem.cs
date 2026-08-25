using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Features.Delivery.Systems
{
    public sealed class RegisterStockedProductSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _stockedProducts;
        private readonly List<GameEntity> _buffer = new(8);

        public RegisterStockedProductSystem(GameContext gameContext,
            IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _stockedProducts = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Product,
                GameMatcher.ProductStocked,
                GameMatcher.DeliveryEntityId,
                GameMatcher.PurchaseOrderLineEntityId,
                GameMatcher.InStock,
                GameMatcher.ProductType));
        }

        public void Execute()
        {
            foreach (GameEntity product in _stockedProducts.GetEntities(_buffer))
            {
                GameEntity delivery =
                    _gameContext.GetEntityWithEntityId(product.DeliveryEntityId);
                if (delivery == null || !delivery.isDelivery ||
                    !delivery.isDeliveryActive || delivery.isDestructed ||
                    !delivery.hasEntityId || !delivery.hasDeliveryPurchaseOrderEntityId ||
                    !delivery.hasDeliveryProductCount ||
                    !delivery.hasStockedProductCount)
                {
                    throw new InvalidOperationException(
                        "A product can only be stocked for an active delivery.");
                }

                GameEntity line = _gameContext.GetEntityWithEntityId(
                    product.PurchaseOrderLineEntityId);
                if (line == null || !line.isPurchaseOrderLine || line.isDestructed ||
                    !line.hasEntityId || !line.hasPurchaseOrderEntityId ||
                    line.PurchaseOrderEntityId !=
                    delivery.DeliveryPurchaseOrderEntityId ||
                    !line.hasProductType || line.ProductType != product.ProductType ||
                    !line.hasPurchaseOrderLineProductCount ||
                    !line.hasPurchaseOrderLineStockedProductCount ||
                    line.PurchaseOrderLineProductCount <= 0 ||
                    line.PurchaseOrderLineStockedProductCount < 0 ||
                    line.PurchaseOrderLineStockedProductCount >=
                    line.PurchaseOrderLineProductCount)
                {
                    throw new InvalidOperationException(
                        $"Stocked product {product.EntityId} does not satisfy purchase " +
                        $"order line {product.PurchaseOrderLineEntityId}.");
                }
                if (product.isInboundProduct)
                {
                    throw new InvalidOperationException(
                        "A stocked delivery product cannot retain InboundProduct.");
                }

                GameEntity order = _gameContext.GetEntityWithEntityId(
                    delivery.DeliveryPurchaseOrderEntityId);
                if (order == null || !order.isPurchaseOrder || order.isDestructed ||
                    !order.hasEntityId || !order.hasStoreEntityId ||
                    !delivery.hasStoreEntityId ||
                    order.StoreEntityId != delivery.StoreEntityId ||
                    order.EntityId != line.PurchaseOrderEntityId)
                {
                    throw new InvalidOperationException(
                        $"Stocked product {product.EntityId} references an invalid purchase " +
                        $"order {delivery.DeliveryPurchaseOrderEntityId}.");
                }

                int stocked = checked(delivery.StockedProductCount + 1);
                int lineStocked = checked(
                    line.PurchaseOrderLineStockedProductCount + 1);
                if (stocked > delivery.DeliveryProductCount)
                {
                    throw new InvalidOperationException(
                        $"Delivery {delivery.EntityId} already registered every product.");
                }

                line.ReplacePurchaseOrderLineStockedProductCount(lineStocked);
                delivery.ReplaceStockedProductCount(stocked);
                product.isProductStocked = false;
                product.RemovePurchaseOrderLineEntityId();
                product.RemoveDeliveryEntityId();
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationProductStocked,
                    stocked,
                    delivery.DeliveryProductCount));
                _events.EmitAudio(AudioCueId.ProductStocked);
            }
        }
    }
}
