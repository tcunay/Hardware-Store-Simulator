using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Features.Delivery.Systems
{
    public sealed class CompleteDeliverySystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _deliveries;
        private readonly List<GameEntity> _deliveryBuffer = new(4);
        private readonly List<GameEntity> _lineBuffer = new(3);

        public CompleteDeliverySystem(GameContext gameContext,
            IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _deliveries = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Delivery,
                    GameMatcher.DeliveryActive,
                    GameMatcher.EntityId,
                    GameMatcher.DeliveryProcurementTerminalEntityId,
                    GameMatcher.DeliveryPurchaseOrderEntityId,
                    GameMatcher.DeliveryProductCount,
                    GameMatcher.StockedProductCount)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity delivery in _deliveries.GetEntities(_deliveryBuffer))
            {
                if (delivery.StockedProductCount < delivery.DeliveryProductCount)
                    continue;
                if (delivery.StockedProductCount > delivery.DeliveryProductCount)
                {
                    throw new InvalidOperationException(
                        $"Delivery {delivery.EntityId} registered more products than expected.");
                }

                GameEntity order = _gameContext.GetEntityWithEntityId(
                    delivery.DeliveryPurchaseOrderEntityId);
                ValidateCompletedManifest(delivery, order);

                delivery.RemoveDeliveryProcurementTerminalEntityId();
                delivery.RemoveDeliveryPurchaseOrderEntityId();
                delivery.isDeliveryActive = false;
                delivery.isDeliveryCompleted = true;

                order.RemovePurchaseOrderProcurementTerminalEntityId();
                foreach (GameEntity line in _lineBuffer)
                    line.isDestructed = true;
                order.isDestructed = true;

                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationDeliveryCompleted));
                _events.EmitAudio(AudioCueId.DeliveryCompleted);
                delivery.isDestructed = true;
            }
        }

        private void ValidateCompletedManifest(GameEntity delivery, GameEntity order)
        {
            if (order == null || !order.isPurchaseOrder || order.isDestructed ||
                !order.hasEntityId || !order.hasStoreEntityId ||
                !delivery.hasStoreEntityId ||
                order.StoreEntityId != delivery.StoreEntityId ||
                !order.hasPurchaseOrderProcurementTerminalEntityId ||
                order.PurchaseOrderProcurementTerminalEntityId !=
                delivery.DeliveryProcurementTerminalEntityId ||
                !order.hasPurchaseOrderProductCount ||
                order.PurchaseOrderProductCount != delivery.DeliveryProductCount ||
                !order.hasPurchaseOrderPackageCount ||
                order.PurchaseOrderPackageCount <= 0 ||
                !order.hasPurchaseOrderCost || !delivery.hasDeliveryCost ||
                order.PurchaseOrderCost != delivery.DeliveryCost ||
                order.PurchaseOrderCost <= 0)
            {
                throw new InvalidOperationException(
                    $"Completed delivery {delivery.EntityId} references an invalid " +
                    "purchase order.");
            }

            _lineBuffer.Clear();
            int packageCount = 0;
            int productCount = 0;
            int cost = 0;
            int stockedProductCount = 0;
            foreach (GameEntity line in _gameContext.GetEntitiesWithPurchaseOrderEntityId(
                         order.EntityId))
            {
                if (!line.isPurchaseOrderLine || line.isDestructed ||
                    !line.hasEntityId || !line.hasProductType ||
                    !line.hasPurchaseOrderEntityId ||
                    line.PurchaseOrderEntityId != order.EntityId ||
                    !line.hasPurchaseOrderLineIndex ||
                    line.PurchaseOrderLineIndex < 0 ||
                    !line.hasPurchaseOrderLinePackageCount ||
                    line.PurchaseOrderLinePackageCount <= 0 ||
                    !line.hasPurchaseOrderLineProductCount ||
                    !line.hasPurchaseOrderLineStockedProductCount ||
                    !line.hasPurchaseOrderLineCost ||
                    line.PurchaseOrderLineCost <= 0 ||
                    line.PurchaseOrderLineProductCount <= 0 ||
                    line.PurchaseOrderLineStockedProductCount !=
                    line.PurchaseOrderLineProductCount)
                {
                    throw new InvalidOperationException(
                        $"Purchase order {order.EntityId} contains an incomplete line.");
                }

                packageCount = checked(
                    packageCount + line.PurchaseOrderLinePackageCount);
                productCount = checked(
                    productCount + line.PurchaseOrderLineProductCount);
                cost = checked(cost + line.PurchaseOrderLineCost);
                stockedProductCount = checked(
                    stockedProductCount +
                    line.PurchaseOrderLineStockedProductCount);
                _lineBuffer.Add(line);
            }

            if (_lineBuffer.Count == 0 ||
                packageCount != order.PurchaseOrderPackageCount ||
                productCount != delivery.DeliveryProductCount ||
                cost != delivery.DeliveryCost ||
                stockedProductCount != delivery.StockedProductCount)
            {
                throw new InvalidOperationException(
                    $"Completed delivery {delivery.EntityId} aggregate counters disagree " +
                    "with its manifest.");
            }
        }
    }
}
