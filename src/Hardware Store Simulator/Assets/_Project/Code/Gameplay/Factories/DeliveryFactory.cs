using System;
using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class DeliveryFactory : IDeliveryFactory
    {
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;
        private readonly GameContext _gameContext;

        public DeliveryFactory(IIdentifierService identifiers, IStaticDataService staticData,
            GameContext gameContext)
        {
            _identifiers = identifiers;
            _staticData = staticData;
            _gameContext = gameContext;
        }

        public GameEntity Create(int purchaseOrderEntityId,
            int procurementTerminalEntityId, int storeEntityId, Pose at)
        {
            GameEntity order = _gameContext.GetEntityWithEntityId(purchaseOrderEntityId);
            if (order == null || !order.isPurchaseOrder || order.isDestructed ||
                !order.hasEntityId || !order.hasPurchaseOrderProcurementTerminalEntityId ||
                order.PurchaseOrderProcurementTerminalEntityId !=
                procurementTerminalEntityId ||
                !order.hasStoreEntityId || order.StoreEntityId != storeEntityId ||
                !order.hasPurchaseOrderPackageCount ||
                !order.hasPurchaseOrderProductCount ||
                !order.hasPurchaseOrderCost ||
                order.PurchaseOrderPackageCount <= 0 ||
                order.PurchaseOrderPackageCount >
                ProcurementCartFactory.CurrentDeliveryPackageCapacity ||
                order.PurchaseOrderProductCount <= 0 ||
                order.PurchaseOrderCost <= 0)
            {
                throw new InvalidOperationException(
                    $"Cannot create a delivery for invalid purchase order " +
                    $"{purchaseOrderEntityId}.");
            }
            GameEntity terminal = _gameContext.GetEntityWithEntityId(
                procurementTerminalEntityId);
            GameEntity store = _gameContext.GetEntityWithEntityId(storeEntityId);
            if (terminal == null || !terminal.isProcurementTerminal ||
                terminal.isDestructed || !terminal.hasEntityId ||
                !terminal.hasStoreEntityId || terminal.StoreEntityId != storeEntityId ||
                store == null || !store.isStore || store.isDestructed ||
                !store.hasEntityId || store.EntityId != order.StoreEntityId)
            {
                throw new InvalidOperationException(
                    $"Purchase order {purchaseOrderEntityId} targets an invalid terminal " +
                    "or store.");
            }
            if (_gameContext.GetEntityWithDeliveryPurchaseOrderEntityId(
                    purchaseOrderEntityId) != null)
            {
                throw new InvalidOperationException(
                    $"Purchase order {purchaseOrderEntityId} already owns a delivery.");
            }
            if (_gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                    procurementTerminalEntityId) != null)
            {
                throw new InvalidOperationException(
                    $"Terminal {procurementTerminalEntityId} already owns a delivery.");
            }

            EntityBehaviour viewPrefab = null;
            int lineCount = 0;
            int lineIndexMask = 0;
            int packageCount = 0;
            int productCount = 0;
            int cost = 0;
            foreach (GameEntity line in _gameContext.GetEntitiesWithPurchaseOrderEntityId(
                         purchaseOrderEntityId))
            {
                ValidateLine(line, purchaseOrderEntityId);
                if (line.PurchaseOrderLineIndex >= 31)
                    throw new InvalidOperationException("Purchase order has too many lines.");
                int lineIndexBit = 1 << line.PurchaseOrderLineIndex;
                if ((lineIndexMask & lineIndexBit) != 0)
                    throw new InvalidOperationException("Purchase order line indexes are not unique.");
                lineIndexMask |= lineIndexBit;

                DeliveryConfig config = _staticData.GetDelivery(line.ProductType);
                if (config.ProductType != line.ProductType)
                {
                    throw new InvalidOperationException(
                        $"Delivery config for {line.ProductType} exposes " +
                        $"{config.ProductType}.");
                }
                if (line.PurchaseOrderLineProductCount != checked(
                        line.PurchaseOrderLinePackageCount * config.ProductCount) ||
                    line.PurchaseOrderLineCost != checked(
                        line.PurchaseOrderLinePackageCount * config.TotalCost))
                {
                    throw new InvalidOperationException(
                        $"Purchase order line {line.EntityId} disagrees with static data.");
                }
                if (viewPrefab == null)
                    viewPrefab = config.ViewPrefab;
                else if (viewPrefab != config.ViewPrefab)
                {
                    throw new InvalidOperationException(
                        "Every product in a mixed delivery must use the same delivery vehicle.");
                }

                lineCount++;
                packageCount = checked(
                    packageCount + line.PurchaseOrderLinePackageCount);
                productCount = checked(
                    productCount + line.PurchaseOrderLineProductCount);
                cost = checked(cost + line.PurchaseOrderLineCost);
            }

            if (lineCount <= 0 || lineCount > order.PurchaseOrderPackageCount ||
                lineIndexMask != (1 << lineCount) - 1 ||
                packageCount != order.PurchaseOrderPackageCount ||
                productCount != order.PurchaseOrderProductCount ||
                cost != order.PurchaseOrderCost)
            {
                throw new InvalidOperationException(
                    $"Purchase order {purchaseOrderEntityId} has an invalid manifest.");
            }

            return CreateEntity.Empty(_identifiers.Next())
                .AddViewPrefab(viewPrefab)
                .AddSpawnPosition(at.position)
                .AddSpawnRotation(at.rotation)
                .AddDeliveryPurchaseOrderEntityId(purchaseOrderEntityId)
                .AddDeliveryProductCount(productCount)
                .AddStockedProductCount(0)
                .AddDeliveryCost(cost)
                .AddStoreEntityId(storeEntityId)
                .With(x => x.isDelivery = true)
                .With(x => x.isDeliveryActive = true)
                .AddDeliveryProcurementTerminalEntityId(procurementTerminalEntityId);
        }

        private static void ValidateLine(GameEntity line, int purchaseOrderEntityId)
        {
            if (!line.isPurchaseOrderLine || line.isDestructed ||
                !line.hasEntityId || !line.hasPurchaseOrderEntityId ||
                line.PurchaseOrderEntityId != purchaseOrderEntityId ||
                !line.hasProductType || !line.hasPurchaseOrderLineIndex ||
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
                    $"Purchase order {purchaseOrderEntityId} contains an invalid line.");
            }
        }
    }
}
