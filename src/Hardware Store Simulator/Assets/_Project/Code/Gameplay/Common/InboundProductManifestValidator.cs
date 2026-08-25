using System;

namespace HardwareStore.Gameplay.Common
{
    internal static class InboundProductManifestValidator
    {
        public static void Validate(GameContext gameContext, GameEntity product)
        {
            if (gameContext == null)
                throw new ArgumentNullException(nameof(gameContext));
            if (product == null || !product.isProduct || product.isDestructed ||
                !product.isInboundProduct || product.isInStock ||
                !product.hasEntityId || !product.hasProductType ||
                !product.hasDeliveryEntityId ||
                !product.hasPurchaseOrderLineEntityId)
            {
                throw new InvalidOperationException(
                    "Inbound manifest validation requires a live inbound product with " +
                    "delivery and purchase-order-line relations.");
            }

            GameEntity delivery = gameContext.GetEntityWithEntityId(
                product.DeliveryEntityId);
            GameEntity line = gameContext.GetEntityWithEntityId(
                product.PurchaseOrderLineEntityId);
            if (delivery == null || !delivery.isDelivery ||
                !delivery.isDeliveryActive || delivery.isDestructed ||
                !delivery.hasEntityId || !delivery.hasStoreEntityId ||
                !delivery.hasDeliveryPurchaseOrderEntityId ||
                line == null || !line.isPurchaseOrderLine || line.isDestructed ||
                !line.hasEntityId || !line.hasPurchaseOrderEntityId ||
                !line.hasProductType || line.ProductType != product.ProductType ||
                line.PurchaseOrderEntityId !=
                delivery.DeliveryPurchaseOrderEntityId)
            {
                throw new InvalidOperationException(
                    $"Inbound product {product.EntityId} has an invalid purchase manifest " +
                    "relation.");
            }

            GameEntity order = gameContext.GetEntityWithEntityId(
                delivery.DeliveryPurchaseOrderEntityId);
            if (order == null || !order.isPurchaseOrder || order.isDestructed ||
                !order.hasEntityId || !order.hasStoreEntityId ||
                order.EntityId != line.PurchaseOrderEntityId ||
                order.StoreEntityId != delivery.StoreEntityId)
            {
                throw new InvalidOperationException(
                    $"Inbound product {product.EntityId} references an invalid purchase " +
                    $"order {delivery.DeliveryPurchaseOrderEntityId}.");
            }
        }
    }
}
