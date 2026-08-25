using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Delivery.Systems
{
    public sealed class SpawnDeliveryProductsSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IProductFactory _productFactory;
        private readonly IStaticDataService _staticData;
        private readonly IGroup<GameEntity> _deliveries;
        private readonly List<GameEntity> _deliveryBuffer = new(4);
        private readonly List<GameEntity> _lineBuffer = new(3);

        public SpawnDeliveryProductsSystem(GameContext gameContext,
            IProductFactory productFactory, IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _productFactory = productFactory;
            _staticData = staticData;
            _deliveries = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Delivery,
                    GameMatcher.DeliveryActive,
                    GameMatcher.EntityId,
                    GameMatcher.DeliveryPurchaseOrderEntityId,
                    GameMatcher.DeliveryProductCount,
                    GameMatcher.Slots)
                .NoneOf(
                    GameMatcher.DeliveryProductsSpawned,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity delivery in _deliveries.GetEntities(_deliveryBuffer))
            {
                ValidateDelivery(delivery);
                if (delivery.Slots.Length < delivery.DeliveryProductCount)
                {
                    throw new InvalidOperationException(
                        $"Delivery {delivery.EntityId} has {delivery.Slots.Length} cargo " +
                        $"slots, but requires {delivery.DeliveryProductCount}.");
                }

                GameEntity order = _gameContext.GetEntityWithEntityId(
                    delivery.DeliveryPurchaseOrderEntityId);
                CollectAndValidateLines(delivery, order);
                ValidateCargoSlots(delivery);
                if (_gameContext.GetEntitiesWithDeliveryEntityId(delivery.EntityId).Count != 0)
                {
                    throw new InvalidOperationException(
                        $"Unspawned delivery {delivery.EntityId} already indexes products.");
                }

                int deliverySlotIndex = 0;
                for (int lineIndex = 0; lineIndex < _lineBuffer.Count; lineIndex++)
                {
                    GameEntity line = _lineBuffer[lineIndex];
                    for (int productIndex = 0;
                         productIndex < line.PurchaseOrderLineProductCount;
                         productIndex++)
                    {
                        Transform slot = delivery.Slots[deliverySlotIndex];
                        GameEntity product = _productFactory.CreateInbound(
                            line.ProductType,
                            new Pose(slot.position, slot.rotation),
                            delivery.EntityId,
                            deliverySlotIndex,
                            line.EntityId);
                        if (product.ProductType != line.ProductType ||
                            product.PurchaseOrderLineEntityId != line.EntityId)
                        {
                            throw new InvalidOperationException(
                                $"Delivery {delivery.EntityId} spawned product " +
                                $"{product.EntityId} outside its manifest.");
                        }
                        deliverySlotIndex++;
                    }
                }
                if (deliverySlotIndex != delivery.DeliveryProductCount)
                {
                    throw new InvalidOperationException(
                        $"Delivery {delivery.EntityId} manifest count changed while spawning.");
                }

                delivery.isDeliveryProductsSpawned = true;
            }
        }

        private static void ValidateDelivery(GameEntity delivery)
        {
            if (!delivery.hasStoreEntityId || !delivery.hasStockedProductCount ||
                !delivery.hasDeliveryCost || delivery.DeliveryProductCount <= 0 ||
                delivery.StockedProductCount != 0 || delivery.DeliveryCost <= 0)
            {
                throw new InvalidOperationException(
                    $"Delivery {delivery.EntityId} has invalid aggregate state.");
            }
        }

        private static void ValidateCargoSlots(GameEntity delivery)
        {
            for (int index = 0; index < delivery.DeliveryProductCount; index++)
            {
                Transform slot = delivery.Slots[index];
                if (slot == null)
                {
                    throw new InvalidOperationException(
                        $"Delivery {delivery.EntityId} cargo slot {index} is missing.");
                }
                for (int previousIndex = 0; previousIndex < index; previousIndex++)
                {
                    if (delivery.Slots[previousIndex] == slot)
                    {
                        throw new InvalidOperationException(
                            $"Delivery {delivery.EntityId} reuses cargo slot transform " +
                            $"{index}.");
                    }
                }
            }
        }

        private void CollectAndValidateLines(GameEntity delivery, GameEntity order)
        {
            if (order == null || !order.isPurchaseOrder || order.isDestructed ||
                !order.hasEntityId || !order.hasStoreEntityId ||
                order.StoreEntityId != delivery.StoreEntityId ||
                !order.hasPurchaseOrderProductCount ||
                order.PurchaseOrderProductCount != delivery.DeliveryProductCount ||
                !order.hasPurchaseOrderPackageCount ||
                order.PurchaseOrderPackageCount <= 0 ||
                !order.hasPurchaseOrderCost ||
                order.PurchaseOrderCost != delivery.DeliveryCost)
            {
                throw new InvalidOperationException(
                    $"Delivery {delivery.EntityId} references an invalid purchase order.");
            }

            _lineBuffer.Clear();
            foreach (GameEntity line in _gameContext.GetEntitiesWithPurchaseOrderEntityId(
                         order.EntityId))
            {
                if (!line.isPurchaseOrderLine || line.isDestructed ||
                    !line.hasEntityId || !line.hasProductType ||
                    !line.hasPurchaseOrderLineIndex ||
                    !line.hasPurchaseOrderLinePackageCount ||
                    !line.hasPurchaseOrderLineProductCount ||
                    !line.hasPurchaseOrderLineCost ||
                    !line.hasPurchaseOrderLineStockedProductCount ||
                    line.PurchaseOrderLineIndex < 0 ||
                    line.PurchaseOrderLinePackageCount <= 0 ||
                    line.PurchaseOrderLineProductCount <= 0 ||
                    line.PurchaseOrderLineCost <= 0 ||
                    line.PurchaseOrderLineStockedProductCount != 0)
                {
                    throw new InvalidOperationException(
                        $"Purchase order {order.EntityId} contains an invalid line.");
                }
                if (_staticData.GetProduct(line.ProductType).ProductType !=
                    line.ProductType)
                {
                    throw new InvalidOperationException(
                        $"Purchase order line {line.EntityId} references mismatched " +
                        "product static data.");
                }
                if (_gameContext.GetEntitiesWithPurchaseOrderLineEntityId(
                        line.EntityId).Count != 0)
                {
                    throw new InvalidOperationException(
                        $"Unspawned purchase order line {line.EntityId} already indexes " +
                        "products.");
                }
                _lineBuffer.Add(line);
            }
            _lineBuffer.Sort(PurchaseOrderLineComparer.Instance);

            int packageCount = 0;
            int productCount = 0;
            int cost = 0;
            for (int index = 0; index < _lineBuffer.Count; index++)
            {
                GameEntity line = _lineBuffer[index];
                if (line.PurchaseOrderLineIndex != index)
                {
                    throw new InvalidOperationException(
                        $"Purchase order {order.EntityId} line indexes are not contiguous.");
                }
                packageCount = checked(
                    packageCount + line.PurchaseOrderLinePackageCount);
                productCount = checked(
                    productCount + line.PurchaseOrderLineProductCount);
                cost = checked(cost + line.PurchaseOrderLineCost);
            }
            if (_lineBuffer.Count == 0 ||
                packageCount != order.PurchaseOrderPackageCount ||
                productCount != delivery.DeliveryProductCount ||
                cost != delivery.DeliveryCost)
            {
                throw new InvalidOperationException(
                    $"Purchase order {order.EntityId} manifest totals are invalid.");
            }
        }

        private sealed class PurchaseOrderLineComparer : IComparer<GameEntity>
        {
            public static readonly PurchaseOrderLineComparer Instance = new();

            public int Compare(GameEntity left, GameEntity right) =>
                left.PurchaseOrderLineIndex.CompareTo(right.PurchaseOrderLineIndex);
        }
    }
}
