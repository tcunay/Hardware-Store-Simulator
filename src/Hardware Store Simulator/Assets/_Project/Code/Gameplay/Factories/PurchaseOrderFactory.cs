using System;
using System.Collections.Generic;
using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class PurchaseOrderFactory : IPurchaseOrderFactory
    {
        private readonly GameContext _gameContext;
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;
        private readonly List<LineDraft> _lineDrafts = new(
            ProcurementCartFactory.CurrentDeliveryPackageCapacity);

        public PurchaseOrderFactory(GameContext gameContext,
            IIdentifierService identifiers, IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public GameEntity Create(int cartEntityId, int procurementTerminalEntityId,
            int storeEntityId)
        {
            ValidateTerminal(procurementTerminalEntityId, storeEntityId);
            GameEntity cart = _gameContext.GetEntityWithEntityId(cartEntityId);
            if (cart == null || !cart.isProcurementCart || cart.isDestructed ||
                !cart.hasEntityId || !cart.hasProcurementCartTerminalEntityId ||
                cart.ProcurementCartTerminalEntityId != procurementTerminalEntityId ||
                !cart.hasStoreEntityId || cart.StoreEntityId != storeEntityId ||
                !cart.hasProcurementCartPackageCapacity ||
                cart.ProcurementCartPackageCapacity <= 0 ||
                cart.ProcurementCartPackageCapacity >
                ProcurementCartFactory.CurrentDeliveryPackageCapacity)
            {
                throw new InvalidOperationException(
                    $"Cannot snapshot invalid procurement cart {cartEntityId}.");
            }

            _lineDrafts.Clear();
            int packageCount = 0;
            foreach (GameEntity line in _gameContext.GetEntitiesWithProcurementCartEntityId(
                         cartEntityId))
            {
                if (line.isDestructed)
                    continue;
                ValidateCartLine(line, cartEntityId);
                DeliveryConfig config = _staticData.GetDelivery(line.ProductType);
                if (config.ProductType != line.ProductType)
                {
                    throw new InvalidOperationException(
                        $"Delivery config for {line.ProductType} exposes " +
                        $"{config.ProductType}.");
                }

                for (int index = 0; index < _lineDrafts.Count; index++)
                {
                    if (_lineDrafts[index].ProductType == line.ProductType)
                    {
                        throw new InvalidOperationException(
                            $"Cart {cartEntityId} contains duplicate {line.ProductType} lines.");
                    }
                }

                packageCount = checked(packageCount + line.ProcurementPackageCount);
                _lineDrafts.Add(CreateDraft(
                    line.ProductType,
                    line.ProcurementPackageCount,
                    config));
            }
            if (_lineDrafts.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Cannot create a purchase order from empty cart {cartEntityId}.");
            }
            if (packageCount > cart.ProcurementCartPackageCapacity)
            {
                throw new InvalidOperationException(
                    $"Cart {cartEntityId} contains {packageCount} packages; capacity is " +
                    $"{cart.ProcurementCartPackageCapacity}.");
            }

            return CreateValidated(
                procurementTerminalEntityId,
                storeEntityId,
                packageCount,
                _lineDrafts);
        }

        private GameEntity CreateValidated(int procurementTerminalEntityId,
            int storeEntityId, int packageCount, List<LineDraft> drafts)
        {
            if (packageCount <= 0 ||
                packageCount > ProcurementCartFactory.CurrentDeliveryPackageCapacity)
            {
                throw new InvalidOperationException(
                    $"A purchase order must contain 1.." +
                    $"{ProcurementCartFactory.CurrentDeliveryPackageCapacity} packages.");
            }
            if (drafts.Count <= 0 || drafts.Count > packageCount)
                throw new InvalidOperationException("Purchase order manifest is invalid.");

            drafts.Sort(LineDraftComparer.Instance);
            int productCount = 0;
            int cost = 0;
            for (int index = 0; index < drafts.Count; index++)
            {
                LineDraft draft = drafts[index];
                if (draft.PackageCount <= 0 || draft.ProductCount <= 0 || draft.Cost <= 0)
                    throw new InvalidOperationException("Purchase order draft is invalid.");
                if (index > 0 && drafts[index - 1].ProductType == draft.ProductType)
                    throw new InvalidOperationException("Purchase order product types must be unique.");

                productCount = checked(productCount + draft.ProductCount);
                cost = checked(cost + draft.Cost);
            }

            int orderEntityId = _identifiers.Next();
            int[] lineEntityIds = new int[drafts.Count];
            for (int index = 0; index < lineEntityIds.Length; index++)
                lineEntityIds[index] = _identifiers.Next();

            GameEntity order = CreateEntity.Empty(orderEntityId)
                .AddPurchaseOrderProcurementTerminalEntityId(
                    procurementTerminalEntityId)
                .AddStoreEntityId(storeEntityId)
                .AddPurchaseOrderPackageCount(packageCount)
                .AddPurchaseOrderProductCount(productCount)
                .AddPurchaseOrderCost(cost)
                .With(x => x.isPurchaseOrder = true);

            for (int index = 0; index < drafts.Count; index++)
            {
                LineDraft draft = drafts[index];
                CreateEntity.Empty(lineEntityIds[index])
                    .AddPurchaseOrderEntityId(orderEntityId)
                    .AddProductType(draft.ProductType)
                    .AddPurchaseOrderLineIndex(index)
                    .AddPurchaseOrderLinePackageCount(draft.PackageCount)
                    .AddPurchaseOrderLineProductCount(draft.ProductCount)
                    .AddPurchaseOrderLineCost(draft.Cost)
                    .AddPurchaseOrderLineStockedProductCount(0)
                    .With(x => x.isPurchaseOrderLine = true);
            }

            return order;
        }

        private void ValidateTerminal(int terminalEntityId, int storeEntityId)
        {
            GameEntity terminal = _gameContext.GetEntityWithEntityId(terminalEntityId);
            GameEntity store = _gameContext.GetEntityWithEntityId(storeEntityId);
            if (terminal == null || !terminal.isProcurementTerminal ||
                terminal.isDestructed || !terminal.hasEntityId ||
                !terminal.hasStoreEntityId ||
                terminal.StoreEntityId != storeEntityId)
            {
                throw new InvalidOperationException(
                    $"Purchase order targets invalid terminal {terminalEntityId}.");
            }
            if (store == null || !store.isStore || !store.hasEntityId ||
                store.isDestructed || store.EntityId != terminal.StoreEntityId ||
                !store.hasProcurementTerminalEntityId ||
                store.ProcurementTerminalEntityId != terminal.EntityId)
            {
                throw new InvalidOperationException(
                    $"Purchase order targets invalid store {storeEntityId}.");
            }
            if (_gameContext.GetEntityWithPurchaseOrderProcurementTerminalEntityId(
                    terminalEntityId) != null)
            {
                throw new InvalidOperationException(
                    $"Terminal {terminalEntityId} already has an active purchase order.");
            }
            if (_gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                    terminalEntityId) != null)
            {
                throw new InvalidOperationException(
                    $"Terminal {terminalEntityId} already has an active delivery.");
            }
        }

        private static void ValidateCartLine(GameEntity line, int cartEntityId)
        {
            if (!line.isProcurementCartLine || !line.hasEntityId ||
                !line.hasProcurementCartEntityId ||
                line.ProcurementCartEntityId != cartEntityId ||
                !line.hasProductType || !line.hasProcurementPackageCount ||
                line.ProcurementPackageCount <= 0)
            {
                throw new InvalidOperationException(
                    $"Cart {cartEntityId} contains an invalid line.");
            }
        }

        private static LineDraft CreateDraft(ProductTypeId productType,
            int packageCount, DeliveryConfig config)
        {
            int productCount = checked(packageCount * config.ProductCount);
            int cost = checked(packageCount * config.TotalCost);
            return new LineDraft(productType, packageCount, productCount, cost);
        }

        private readonly struct LineDraft
        {
            public LineDraft(ProductTypeId productType, int packageCount,
                int productCount, int cost)
            {
                ProductType = productType;
                PackageCount = packageCount;
                ProductCount = productCount;
                Cost = cost;
            }

            public ProductTypeId ProductType { get; }
            public int PackageCount { get; }
            public int ProductCount { get; }
            public int Cost { get; }
        }

        private sealed class LineDraftComparer : IComparer<LineDraft>
        {
            public static readonly LineDraftComparer Instance = new();

            public int Compare(LineDraft x, LineDraft y) =>
                ((int)x.ProductType).CompareTo((int)y.ProductType);
        }
    }
}
