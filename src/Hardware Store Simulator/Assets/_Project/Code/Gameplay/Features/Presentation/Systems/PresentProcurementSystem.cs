using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Economy;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Presentation;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Presentation.Systems
{
    public sealed class PresentProcurementSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IStaticDataService _staticData;
        private readonly IProcurementSolvencyService _solvency;
        private readonly IHudService _hud;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<GameEntity> _stockedProducts;
        private readonly ProductTypeId[] _productTypes;
        private readonly int[] _stockProductCountBuffer;
        private readonly List<GameEntity> _playerBuffer = new(1);
        private readonly List<GameEntity> _cartLineEntityBuffer = new(8);
        private readonly List<GameEntity> _orderLineEntityBuffer = new(8);
        private readonly List<GameEntity> _consultationOfferEntityBuffer = new(3);
        private readonly List<GameEntity> _consultationLineEntityBuffer = new(3);
        private readonly List<ExactDemandLine> _exactDemandLineBuffer = new(3);
        private readonly List<GameEntity> _visitBuffer = new(8);
        private readonly List<int> _sourceFingerprint = new(128);
        private readonly List<int> _cachedFingerprint = new(128);
        private ProcurementSnapshot _cachedSnapshot;
        private bool _hasCachedSnapshot;

        public PresentProcurementSystem(GameContext gameContext,
            IStaticDataService staticData,
            IProcurementSolvencyService solvency,
            IHudService hud)
        {
            _gameContext = gameContext;
            _staticData = staticData;
            _solvency = solvency;
            _hud = hud;
            _productTypes = new ProductTypeId[staticData.ProductTypes.Count];
            for (int index = 0; index < _productTypes.Length; index++)
                _productTypes[index] = staticData.ProductTypes[index];
            if (_productTypes.Length == 0)
            {
                throw new InvalidOperationException(
                    "The procurement catalog requires at least one product type.");
            }
            _stockProductCountBuffer = new int[_productTypes.Length];
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.ModalOpen,
                GameMatcher.ProcurementTerminalEntityId));
            _stockedProducts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.InStock,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.ProductType)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            _hud.PresentProcurement(null);

            List<GameEntity> players = _players.GetEntities(_playerBuffer);
            if (players.Count > 1)
            {
                throw new InvalidOperationException(
                    "The local HUD cannot present more than one procurement catalog at a time.");
            }
            if (players.Count == 0)
            {
                _hasCachedSnapshot = false;
                return;
            }

            GameEntity player = players[0];
            if (player.hasConsultationVisitEntityId)
            {
                throw new InvalidOperationException(
                    "Consultation and procurement modals cannot be open at the same time.");
            }

            GameEntity terminal = _gameContext.GetEntityWithEntityId(
                player.ProcurementTerminalEntityId);
            ValidateTerminal(player, terminal);

            GameEntity store = _gameContext.GetEntityWithEntityId(terminal.StoreEntityId);
            GameEntity storageZone = _gameContext.GetEntityWithEntityId(
                terminal.StorageZoneEntityId);
            ValidateStoreAndStorage(terminal, store, storageZone);

            if (_gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                    terminal.EntityId) != null)
            {
                throw new InvalidOperationException(
                    $"Procurement catalog for terminal {terminal.EntityId} cannot remain open " +
                    "while a delivery is active.");
            }

            int selectedProductIndex = Array.IndexOf(
                _productTypes,
                terminal.SelectedProductType);
            if (selectedProductIndex < 0)
            {
                throw new InvalidOperationException(
                    $"Selected product {terminal.SelectedProductType} is absent from static data.");
            }

            GameEntity cart = _gameContext.GetEntityWithProcurementCartTerminalEntityId(
                terminal.EntityId);
            ValidateCart(cart, terminal, store);
            CollectCartLineEntities(cart);
            CaptureSourceFingerprint(
                player,
                terminal,
                store,
                storageZone,
                cart);
            if (_hasCachedSnapshot && SourceFingerprintMatchesCache())
            {
                _hud.PresentProcurement(_cachedSnapshot);
                return;
            }

            ProcurementCartLineSnapshot[] cartLines = CreateCartLines(cart);
            ProcurementPurchaseEvaluation evaluation = cartLines.Length == 0
                ? _solvency.EvaluatePurchase(
                    terminal.EntityId,
                    terminal.SelectedProductType)
                : _solvency.EvaluateCart(cart.EntityId);

            _exactDemandLineBuffer.Clear();
            switch (evaluation.DemandKind)
            {
                case ProcurementDemandKind.ProjectForecast:
                    break;
                case ProcurementDemandKind.SelectedCustomerOrder:
                    CollectSelectedCustomerOrderLines(
                        ResolveDemandVisit(evaluation),
                        store,
                        storageZone,
                        evaluation.ProjectType);
                    break;
                case ProcurementDemandKind.ConfirmedOrder:
                    CollectConfirmedOrderLines(
                        ResolveDemandVisit(evaluation),
                        store,
                        storageZone,
                        evaluation.ProjectType);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(evaluation.DemandKind),
                        evaluation.DemandKind,
                        null);
            }

            CustomerProjectConfig project = _staticData.GetProject(
                evaluation.ProjectType);
            int freeStorageSlotCount = checked(
                storageZone.Slots.Length - storageZone.OccupiedStorageSlotCount);
            var products = new ProcurementProductSnapshot[_productTypes.Length];
            for (int index = 0; index < _productTypes.Length; index++)
            {
                products[index] = CreateProductSnapshot(
                    index,
                    _productTypes[index],
                    terminal,
                    evaluation.DemandKind,
                    project,
                    _exactDemandLineBuffer,
                    cartLines);
            }

            ProcurementCartSnapshot cartSnapshot = CreateCartSnapshot(
                cart,
                cartLines,
                store,
                evaluation);
            var snapshot = new ProcurementSnapshot(
                evaluation.DemandKind,
                evaluation.ProjectType,
                store.Money,
                freeStorageSlotCount,
                selectedProductIndex,
                products,
                cartSnapshot);
            CacheSourceFingerprint(snapshot);
            _hud.PresentProcurement(snapshot);
        }

        private ProcurementProductSnapshot CreateProductSnapshot(
            int index,
            ProductTypeId productType,
            GameEntity terminal,
            ProcurementDemandKind demandKind,
            CustomerProjectConfig project,
            IReadOnlyList<ExactDemandLine> exactDemandLines,
            ProcurementCartLineSnapshot[] cartLines)
        {
            ExactDemandLine? exactDemandLine = null;
            for (int lineIndex = 0;
                 lineIndex < exactDemandLines.Count;
                 lineIndex++)
            {
                if (exactDemandLines[lineIndex].ProductType != productType)
                    continue;

                exactDemandLine = exactDemandLines[lineIndex];
                break;
            }

            int stockProductCount = _stockProductCountBuffer[index];
            if (exactDemandLine.HasValue &&
                exactDemandLine.Value.AvailableProductCount != stockProductCount)
            {
                throw new InvalidOperationException(
                    $"Customer demand line {exactDemandLine.Value.EntityId} has stale " +
                    "product availability.");
            }

            int remainingRequiredProductCount = !exactDemandLine.HasValue
                ? 0
                : exactDemandLine.Value.RemainingRequiredProductCount;
            int inTransitProductCount = CountInTransitProducts(
                terminal.EntityId,
                productType);
            DeliveryConfig delivery = _staticData.GetDelivery(productType);
            int cartPackageCount = FindCartPackageCount(cartLines, productType);
            int cartProductCount = checked(cartPackageCount * delivery.ProductCount);
            int projectedDeficitProductCount = demandKind !=
                                              ProcurementDemandKind.ProjectForecast
                ? Math.Max(
                    0,
                    remainingRequiredProductCount - stockProductCount -
                    inTransitProductCount - cartProductCount)
                : 0;
            ResolveDemandRange(
                demandKind,
                project,
                productType,
                remainingRequiredProductCount,
                out int minimumRequiredProductCount,
                out int maximumRequiredProductCount);

            return new ProcurementProductSnapshot(
                index,
                productType,
                delivery.ProductCount,
                delivery.TotalCost,
                stockProductCount,
                inTransitProductCount,
                minimumRequiredProductCount,
                maximumRequiredProductCount,
                remainingRequiredProductCount,
                projectedDeficitProductCount,
                cartPackageCount);
        }

        private ProcurementCartLineSnapshot[] CreateCartLines(GameEntity cart)
        {
            var lines = new ProcurementCartLineSnapshot[
                _cartLineEntityBuffer.Count];
            for (int index = 0; index < _cartLineEntityBuffer.Count; index++)
            {
                GameEntity line = _cartLineEntityBuffer[index];
                ValidateCartLine(line, cart);
                DeliveryConfig delivery = _staticData.GetDelivery(line.ProductType);
                int productCount = checked(
                    line.ProcurementPackageCount * delivery.ProductCount);
                int lineCost = checked(
                    line.ProcurementPackageCount * delivery.TotalCost);
                lines[index] = new ProcurementCartLineSnapshot(
                    index,
                    line.ProductType,
                    line.ProcurementPackageCount,
                    delivery.ProductCount,
                    delivery.TotalCost,
                    productCount,
                    lineCost);
            }

            return lines;
        }

        private void CollectCartLineEntities(GameEntity cart)
        {
            _cartLineEntityBuffer.Clear();
            foreach (GameEntity line in
                     _gameContext.GetEntitiesWithProcurementCartEntityId(cart.EntityId))
            {
                if (!line.isDestructed)
                    _cartLineEntityBuffer.Add(line);
            }
            _cartLineEntityBuffer.Sort(CartLineComparer.Instance);

            for (int index = 0; index < _cartLineEntityBuffer.Count; index++)
            {
                ValidateCartLine(_cartLineEntityBuffer[index], cart);
                if (index > 0 &&
                    _cartLineEntityBuffer[index - 1].ProductType ==
                    _cartLineEntityBuffer[index].ProductType)
                {
                    throw new InvalidOperationException(
                        $"Procurement cart {cart.EntityId} contains duplicate " +
                        $"{_cartLineEntityBuffer[index].ProductType} lines.");
                }
            }
        }

        private static ProcurementCartSnapshot CreateCartSnapshot(
            GameEntity cart,
            ProcurementCartLineSnapshot[] lines,
            GameEntity store,
            ProcurementPurchaseEvaluation evaluation)
        {
            int packageCount = 0;
            int productCount = 0;
            int totalCost = 0;
            for (int index = 0; index < lines.Length; index++)
            {
                packageCount = checked(packageCount + lines[index].PackageCount);
                productCount = checked(productCount + lines[index].ProductCount);
                totalCost = checked(totalCost + lines[index].LineCost);
            }

            ProcurementPurchaseState purchaseState = lines.Length == 0
                ? ProcurementPurchaseState.Available
                : MapPurchaseState(evaluation.Availability);
            int moneyAfterPurchase = lines.Length == 0
                ? store.Money
                : evaluation.MoneyAfterPurchase;
            if (lines.Length > 0 &&
                (evaluation.DeliveryProductCount != productCount ||
                 evaluation.DeliveryCost != totalCost))
            {
                throw new InvalidOperationException(
                    $"Procurement cart {cart.EntityId} disagrees with its solvency evaluation.");
            }

            return new ProcurementCartSnapshot(
                lines,
                packageCount,
                cart.ProcurementCartPackageCapacity,
                productCount,
                totalCost,
                moneyAfterPurchase,
                productCount,
                purchaseState);
        }

        private void CaptureSourceFingerprint(
            GameEntity player,
            GameEntity terminal,
            GameEntity store,
            GameEntity storageZone,
            GameEntity cart)
        {
            _sourceFingerprint.Clear();
            AddFingerprintValue(player.EntityId);
            AddFingerprintValue(player.ProcurementTerminalEntityId);
            AddFingerprintValue(terminal.EntityId);
            AddFingerprintValue(terminal.StoreEntityId);
            AddFingerprintValue(terminal.StorageZoneEntityId);
            AddFingerprintValue((int)terminal.SelectedProductType);
            AddFingerprintValue(cart.EntityId);
            AddFingerprintValue(cart.ProcurementCartPackageCapacity);
            AddFingerprintValue(store.EntityId);
            AddFingerprintValue(store.Money);
            AddFingerprintValue(store.NextProjectSequenceIndex);
            AddFingerprintValue(store.NextCustomerArrivalSequence);
            AddFingerprintValue(storageZone.EntityId);
            AddFingerprintValue(storageZone.Slots.Length);
            AddFingerprintValue(storageZone.OccupiedStorageSlotCount);

            Array.Clear(
                _stockProductCountBuffer,
                0,
                _stockProductCountBuffer.Length);
            int countedStock = 0;
            foreach (GameEntity product in _stockedProducts)
            {
                if (product.StorageZoneEntityId != storageZone.EntityId)
                    continue;

                int productIndex = Array.IndexOf(
                    _productTypes,
                    product.ProductType);
                if (productIndex < 0)
                {
                    throw new InvalidOperationException(
                        $"Storage contains unconfigured product {product.ProductType}.");
                }
                _stockProductCountBuffer[productIndex] = checked(
                    _stockProductCountBuffer[productIndex] + 1);
                countedStock++;
            }
            if (countedStock != storageZone.OccupiedStorageSlotCount)
            {
                throw new InvalidOperationException(
                    $"Storage zone {storageZone.EntityId} reports " +
                    $"{storageZone.OccupiedStorageSlotCount} occupied slots, but " +
                    $"{countedStock} stocked products were found.");
            }
            AddFingerprintValue(_productTypes.Length);
            for (int index = 0; index < _productTypes.Length; index++)
            {
                AddFingerprintValue((int)_productTypes[index]);
                AddFingerprintValue(_stockProductCountBuffer[index]);
            }

            AddFingerprintValue(_cartLineEntityBuffer.Count);
            for (int index = 0; index < _cartLineEntityBuffer.Count; index++)
            {
                GameEntity line = _cartLineEntityBuffer[index];
                AddFingerprintValue(line.EntityId);
                AddFingerprintValue(line.ProcurementCartEntityId);
                AddFingerprintValue((int)line.ProductType);
                AddFingerprintValue(line.ProcurementPackageCount);
            }

            CapturePurchaseOrderFingerprint(terminal.EntityId);
            CaptureVisitFingerprints(store);
        }

        private void CapturePurchaseOrderFingerprint(int terminalEntityId)
        {
            GameEntity purchaseOrder =
                _gameContext.GetEntityWithPurchaseOrderProcurementTerminalEntityId(
                    terminalEntityId);
            AddFingerprintValue(purchaseOrder == null ? 0 : 1);
            if (purchaseOrder == null)
                return;

            AddFingerprintValue(purchaseOrder.EntityId);
            AddFingerprintValue(purchaseOrder.isDestructed ? 1 : 0);
            AddFingerprintValue(purchaseOrder.hasStoreEntityId
                ? purchaseOrder.StoreEntityId
                : int.MinValue);
            AddFingerprintValue(
                purchaseOrder.hasPurchaseOrderProcurementTerminalEntityId
                    ? purchaseOrder.PurchaseOrderProcurementTerminalEntityId
                    : int.MinValue);
            AddFingerprintValue(purchaseOrder.hasPurchaseOrderPackageCount
                ? purchaseOrder.PurchaseOrderPackageCount
                : int.MinValue);
            AddFingerprintValue(purchaseOrder.hasPurchaseOrderProductCount
                ? purchaseOrder.PurchaseOrderProductCount
                : int.MinValue);
            AddFingerprintValue(purchaseOrder.hasPurchaseOrderCost
                ? purchaseOrder.PurchaseOrderCost
                : int.MinValue);

            _orderLineEntityBuffer.Clear();
            foreach (GameEntity line in
                     _gameContext.GetEntitiesWithPurchaseOrderEntityId(
                         purchaseOrder.EntityId))
            {
                _orderLineEntityBuffer.Add(line);
            }
            _orderLineEntityBuffer.Sort(PurchaseOrderLineComparer.Instance);
            AddFingerprintValue(_orderLineEntityBuffer.Count);
            for (int index = 0; index < _orderLineEntityBuffer.Count; index++)
            {
                GameEntity line = _orderLineEntityBuffer[index];
                AddFingerprintValue(line.hasEntityId ? line.EntityId : int.MinValue);
                AddFingerprintValue(line.isDestructed ? 1 : 0);
                AddFingerprintValue(line.hasPurchaseOrderLineIndex
                    ? line.PurchaseOrderLineIndex
                    : int.MinValue);
                AddFingerprintValue(line.hasProductType
                    ? (int)line.ProductType
                    : int.MinValue);
                AddFingerprintValue(line.hasPurchaseOrderLinePackageCount
                    ? line.PurchaseOrderLinePackageCount
                    : int.MinValue);
                AddFingerprintValue(line.hasPurchaseOrderLineProductCount
                    ? line.PurchaseOrderLineProductCount
                    : int.MinValue);
                AddFingerprintValue(line.hasPurchaseOrderLineCost
                    ? line.PurchaseOrderLineCost
                    : int.MinValue);
                AddFingerprintValue(line.hasPurchaseOrderLineStockedProductCount
                    ? line.PurchaseOrderLineStockedProductCount
                    : int.MinValue);
                AddFingerprintValue(line.hasPurchaseOrderEntityId
                    ? line.PurchaseOrderEntityId
                    : int.MinValue);
            }
        }

        private void CaptureVisitFingerprints(GameEntity store)
        {
            _visitBuffer.Clear();
            foreach (GameEntity visit in
                     _gameContext.GetEntitiesWithCustomerVisitStoreEntityId(
                         store.EntityId))
            {
                _visitBuffer.Add(visit);
            }
            _visitBuffer.Sort(CustomerVisitComparer.Instance);
            AddFingerprintValue(_visitBuffer.Count);
            for (int visitIndex = 0; visitIndex < _visitBuffer.Count; visitIndex++)
            {
                GameEntity visit = _visitBuffer[visitIndex];
                AddFingerprintValue(visit.hasEntityId ? visit.EntityId : int.MinValue);
                AddFingerprintValue(visit.isDestructed ? 1 : 0);
                AddFingerprintValue(visit.hasCustomerArrivalSequence
                    ? visit.CustomerArrivalSequence
                    : int.MinValue);
                AddFingerprintValue(visit.hasCustomerProjectType
                    ? (int)visit.CustomerProjectType
                    : int.MinValue);
                AddFingerprintValue(visit.hasCustomerVisitStoreEntityId
                    ? visit.CustomerVisitStoreEntityId
                    : int.MinValue);
                AddFingerprintValue(visit.hasStorageZoneEntityId
                    ? visit.StorageZoneEntityId
                    : int.MinValue);
                AddFingerprintValue(ResolveLifecycleMask(visit));
                AddFingerprintValue(visit.isOrder ? 1 : 0);
                AddFingerprintValue(visit.isOrderRewarded ? 1 : 0);
                AddFingerprintValue(visit.hasOrderReward
                    ? visit.OrderReward
                    : int.MinValue);
                AddFingerprintValue(visit.isOrderContentReleased ? 1 : 0);
                AddFingerprintValue(visit.hasServingOrderCounterEntityId
                    ? visit.ServingOrderCounterEntityId
                    : int.MinValue);
                AddFingerprintValue(visit.hasReservedCustomerLoadingBayEntityId
                    ? visit.ReservedCustomerLoadingBayEntityId
                    : int.MinValue);
                CaptureConsultationOffersFingerprint(visit);

                _orderLineEntityBuffer.Clear();
                foreach (GameEntity line in
                         _gameContext.GetEntitiesWithOrderEntityId(visit.EntityId))
                {
                    _orderLineEntityBuffer.Add(line);
                }
                _orderLineEntityBuffer.Sort(OrderLineComparer.Instance);
                AddFingerprintValue(_orderLineEntityBuffer.Count);
                for (int lineIndex = 0;
                     lineIndex < _orderLineEntityBuffer.Count;
                     lineIndex++)
                {
                    GameEntity line = _orderLineEntityBuffer[lineIndex];
                    AddFingerprintValue(line.hasEntityId
                        ? line.EntityId
                        : int.MinValue);
                    AddFingerprintValue(line.isDestructed ? 1 : 0);
                    AddFingerprintValue(line.hasLineIndex
                        ? line.LineIndex
                        : int.MinValue);
                    AddFingerprintValue(line.hasProductType
                        ? (int)line.ProductType
                        : int.MinValue);
                    AddFingerprintValue(line.hasRequiredProductCount
                        ? line.RequiredProductCount
                        : int.MinValue);
                    AddFingerprintValue(line.hasAvailableProductCount
                        ? line.AvailableProductCount
                        : int.MinValue);
                    AddFingerprintValue(line.hasLoadedProductCount
                        ? line.LoadedProductCount
                        : int.MinValue);
                    AddFingerprintValue(line.hasOrderEntityId
                        ? line.OrderEntityId
                        : int.MinValue);
                    AddFingerprintValue(line.hasStorageZoneEntityId
                        ? line.StorageZoneEntityId
                        : int.MinValue);
                }
            }
        }

        private void CaptureConsultationOffersFingerprint(GameEntity visit)
        {
            _consultationOfferEntityBuffer.Clear();
            foreach (GameEntity offer in
                     _gameContext.GetEntitiesWithConsultationOfferVisitEntityId(
                         visit.EntityId))
            {
                _consultationOfferEntityBuffer.Add(offer);
            }
            _consultationOfferEntityBuffer.Sort(ConsultationOfferComparer.Instance);
            AddFingerprintValue(_consultationOfferEntityBuffer.Count);
            for (int offerIndex = 0;
                 offerIndex < _consultationOfferEntityBuffer.Count;
                 offerIndex++)
            {
                GameEntity offer = _consultationOfferEntityBuffer[offerIndex];
                AddFingerprintValue(offer.hasEntityId
                    ? offer.EntityId
                    : int.MinValue);
                AddFingerprintValue(offer.isDestructed ? 1 : 0);
                AddFingerprintValue(offer.isConsultationOffer ? 1 : 0);
                AddFingerprintValue(offer.isSelectedConsultationOffer ? 1 : 0);
                AddFingerprintValue(offer.hasOfferIndex
                    ? offer.OfferIndex
                    : int.MinValue);
                AddFingerprintValue(offer.hasOrderReward
                    ? offer.OrderReward
                    : int.MinValue);
                AddFingerprintValue(offer.hasExpectedProfit
                    ? offer.ExpectedProfit
                    : int.MinValue);
                AddFingerprintValue(offer.hasConsultationOfferVisitEntityId
                    ? offer.ConsultationOfferVisitEntityId
                    : int.MinValue);

                _consultationLineEntityBuffer.Clear();
                if (offer.hasEntityId)
                {
                    foreach (GameEntity line in
                             _gameContext.GetEntitiesWithConsultationOfferEntityId(
                                 offer.EntityId))
                    {
                        _consultationLineEntityBuffer.Add(line);
                    }
                }
                _consultationLineEntityBuffer.Sort(OrderLineComparer.Instance);
                AddFingerprintValue(_consultationLineEntityBuffer.Count);
                for (int lineIndex = 0;
                     lineIndex < _consultationLineEntityBuffer.Count;
                     lineIndex++)
                {
                    GameEntity line = _consultationLineEntityBuffer[lineIndex];
                    AddFingerprintValue(line.hasEntityId
                        ? line.EntityId
                        : int.MinValue);
                    AddFingerprintValue(line.isDestructed ? 1 : 0);
                    AddFingerprintValue(line.isConsultationOfferLine ? 1 : 0);
                    AddFingerprintValue(line.hasLineIndex
                        ? line.LineIndex
                        : int.MinValue);
                    AddFingerprintValue(line.hasProductType
                        ? (int)line.ProductType
                        : int.MinValue);
                    AddFingerprintValue(line.hasRequiredProductCount
                        ? line.RequiredProductCount
                        : int.MinValue);
                    AddFingerprintValue(line.hasAvailableProductCount
                        ? line.AvailableProductCount
                        : int.MinValue);
                    AddFingerprintValue(line.hasConsultationOfferEntityId
                        ? line.ConsultationOfferEntityId
                        : int.MinValue);
                    AddFingerprintValue(line.hasStorageZoneEntityId
                        ? line.StorageZoneEntityId
                        : int.MinValue);
                }
            }
        }

        private bool SourceFingerprintMatchesCache()
        {
            if (_sourceFingerprint.Count != _cachedFingerprint.Count)
                return false;
            for (int index = 0; index < _sourceFingerprint.Count; index++)
            {
                if (_sourceFingerprint[index] != _cachedFingerprint[index])
                    return false;
            }

            return true;
        }

        private void CacheSourceFingerprint(ProcurementSnapshot snapshot)
        {
            _cachedFingerprint.Clear();
            for (int index = 0; index < _sourceFingerprint.Count; index++)
                _cachedFingerprint.Add(_sourceFingerprint[index]);
            _cachedSnapshot = snapshot;
            _hasCachedSnapshot = true;
        }

        private void AddFingerprintValue(int value) =>
            _sourceFingerprint.Add(value);

        private static int ResolveLifecycleMask(GameEntity visit)
        {
            int mask = 0;
            if (visit.isCustomerVisitArriving) mask |= 1 << 0;
            if (visit.isCustomerVisitQueued) mask |= 1 << 1;
            if (visit.isCustomerVisitConsulting) mask |= 1 << 2;
            if (visit.isCustomerVisitWaitingForLoadingBay) mask |= 1 << 3;
            if (visit.isCustomerVisitMovingToLoadingBay) mask |= 1 << 4;
            if (visit.isCustomerVisitLoading) mask |= 1 << 5;
            if (visit.isCustomerVisitCompleted) mask |= 1 << 6;
            if (visit.isCustomerVisitReturning) mask |= 1 << 7;
            if (visit.isCustomerVisitDeparting) mask |= 1 << 8;
            if (visit.isCustomerVisitAbandoning) mask |= 1 << 9;
            if (visit.isCustomerVisitWaitingForAbandonDeparture) mask |= 1 << 10;
            if (visit.isCustomerVisitAbandonDeparting) mask |= 1 << 11;
            return mask;
        }

        private int CountInTransitProducts(int terminalEntityId,
            ProductTypeId productType)
        {
            GameEntity purchaseOrder =
                _gameContext.GetEntityWithPurchaseOrderProcurementTerminalEntityId(
                    terminalEntityId);
            if (purchaseOrder == null)
                return 0;
            if (!purchaseOrder.isPurchaseOrder || purchaseOrder.isDestructed ||
                !purchaseOrder.hasEntityId)
            {
                throw new InvalidOperationException(
                    $"Terminal {terminalEntityId} owns an invalid purchase order.");
            }

            int count = 0;
            foreach (GameEntity line in
                     _gameContext.GetEntitiesWithPurchaseOrderEntityId(
                         purchaseOrder.EntityId))
            {
                if (line.isDestructed)
                    continue;
                if (!line.isPurchaseOrderLine || !line.hasProductType ||
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
                if (line.ProductType != productType)
                    continue;

                count = checked(
                    count + line.PurchaseOrderLineProductCount -
                    line.PurchaseOrderLineStockedProductCount);
            }

            return count;
        }

        private static int FindCartPackageCount(
            ProcurementCartLineSnapshot[] lines,
            ProductTypeId productType)
        {
            for (int index = 0; index < lines.Length; index++)
            {
                if (lines[index].ProductType == productType)
                    return lines[index].PackageCount;
            }

            return 0;
        }

        private static void ResolveDemandRange(
            ProcurementDemandKind demandKind,
            CustomerProjectConfig project,
            ProductTypeId productType,
            int remainingRequiredProductCount,
            out int minimumRequiredProductCount,
            out int maximumRequiredProductCount)
        {
            if (demandKind == ProcurementDemandKind.ConfirmedOrder ||
                demandKind == ProcurementDemandKind.SelectedCustomerOrder)
            {
                minimumRequiredProductCount = remainingRequiredProductCount;
                maximumRequiredProductCount = remainingRequiredProductCount;
                return;
            }
            if (demandKind != ProcurementDemandKind.ProjectForecast)
                throw new ArgumentOutOfRangeException(nameof(demandKind), demandKind, null);

            minimumRequiredProductCount = int.MaxValue;
            maximumRequiredProductCount = 0;
            foreach (CustomerProjectOfferDefinition offer in project.Offers)
            {
                int requiredCount = 0;
                foreach (CustomerProjectLineDefinition line in offer.Lines)
                {
                    if (line.ProductType == productType)
                    {
                        requiredCount = line.RequiredCount;
                        break;
                    }
                }

                minimumRequiredProductCount = Math.Min(
                    minimumRequiredProductCount,
                    requiredCount);
                maximumRequiredProductCount = Math.Max(
                    maximumRequiredProductCount,
                    requiredCount);
            }

            if (minimumRequiredProductCount == int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Project {project.ProjectType} has no procurement offers.");
            }
        }

        private static ProcurementPurchaseState MapPurchaseState(
            ProcurementPurchaseAvailability availability) =>
            availability switch
            {
                ProcurementPurchaseAvailability.Available =>
                    ProcurementPurchaseState.Available,
                ProcurementPurchaseAvailability.InsufficientStorage =>
                    ProcurementPurchaseState.InsufficientStorage,
                ProcurementPurchaseAvailability.InsufficientMoney =>
                    ProcurementPurchaseState.InsufficientMoney,
                ProcurementPurchaseAvailability.DemandWouldBecomeInsolvent =>
                    ProcurementPurchaseState.PlanWouldBecomeUnfulfillable,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(availability), availability, null)
            };

        private static void ValidateTerminal(GameEntity player, GameEntity terminal)
        {
            if (terminal == null || !terminal.isProcurementTerminal ||
                !terminal.hasEntityId || !terminal.hasStoreEntityId ||
                !terminal.hasStorageZoneEntityId ||
                !terminal.hasSelectedProductType ||
                terminal.EntityId != player.ProcurementTerminalEntityId)
            {
                throw new InvalidOperationException(
                    $"Player procurement modal references invalid terminal " +
                    $"{player.ProcurementTerminalEntityId}.");
            }
        }

        private static void ValidateStoreAndStorage(
            GameEntity terminal,
            GameEntity store,
            GameEntity storageZone)
        {
            if (store == null || !store.isStore || !store.hasEntityId ||
                !store.hasMoney || !store.hasNextProjectSequenceIndex ||
                !store.hasNextCustomerArrivalSequence ||
                !store.hasProcurementTerminalEntityId ||
                !store.hasStorageZoneEntityId || store.Money < 0 ||
                store.NextCustomerArrivalSequence < 0 ||
                store.ProcurementTerminalEntityId != terminal.EntityId ||
                store.StorageZoneEntityId != storageZone?.EntityId ||
                terminal.StoreEntityId != store.EntityId)
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminal.EntityId} references an invalid store.");
            }
            if (storageZone == null || !storageZone.isStorageZone ||
                !storageZone.hasEntityId || !storageZone.hasSlots ||
                !storageZone.hasOccupiedStorageSlotCount ||
                storageZone.OccupiedStorageSlotCount < 0 ||
                storageZone.OccupiedStorageSlotCount > storageZone.Slots.Length ||
                terminal.StorageZoneEntityId != storageZone.EntityId)
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminal.EntityId} references an invalid " +
                    "storage zone.");
            }
        }

        private static void ValidateCart(
            GameEntity cart,
            GameEntity terminal,
            GameEntity store)
        {
            if (cart == null || !cart.isProcurementCart || cart.isDestructed ||
                !cart.hasEntityId || !cart.hasProcurementCartTerminalEntityId ||
                cart.ProcurementCartTerminalEntityId != terminal.EntityId ||
                !cart.hasStoreEntityId || cart.StoreEntityId != store.EntityId ||
                !cart.hasProcurementCartPackageCapacity ||
                cart.ProcurementCartPackageCapacity <= 0)
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminal.EntityId} owns no valid cart.");
            }
        }

        private static void ValidateCartLine(GameEntity line, GameEntity cart)
        {
            if (!line.isProcurementCartLine || !line.hasEntityId ||
                !line.hasProcurementCartEntityId ||
                line.ProcurementCartEntityId != cart.EntityId ||
                !line.hasProductType || !line.hasProcurementPackageCount ||
                line.ProcurementPackageCount <= 0)
            {
                throw new InvalidOperationException(
                    $"Procurement cart {cart.EntityId} contains an invalid line.");
            }
        }

        private GameEntity ResolveDemandVisit(
            ProcurementPurchaseEvaluation evaluation)
        {
            if (!evaluation.DemandVisitEntityId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Exact procurement demand {evaluation.DemandKind} does not " +
                    "identify its customer visit.");
            }

            return _gameContext.GetEntityWithEntityId(
                evaluation.DemandVisitEntityId.Value);
        }

        private void CollectSelectedCustomerOrderLines(
            GameEntity visit,
            GameEntity store,
            GameEntity storageZone,
            CustomerProjectTypeId projectType)
        {
            ValidateSelectedCustomerOrder(
                visit,
                store,
                storageZone,
                projectType);

            _consultationOfferEntityBuffer.Clear();
            foreach (GameEntity offer in
                     _gameContext.GetEntitiesWithConsultationOfferVisitEntityId(
                         visit.EntityId))
            {
                if (!offer.isDestructed)
                    _consultationOfferEntityBuffer.Add(offer);
            }
            if (_consultationOfferEntityBuffer.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Selected customer order {visit.EntityId} must own exactly one " +
                    "active consultation offer.");
            }

            GameEntity selectedOffer = _consultationOfferEntityBuffer[0];
            if (!selectedOffer.isConsultationOffer ||
                !selectedOffer.isSelectedConsultationOffer ||
                !selectedOffer.hasEntityId ||
                !selectedOffer.hasConsultationOfferVisitEntityId ||
                selectedOffer.ConsultationOfferVisitEntityId != visit.EntityId ||
                !selectedOffer.hasOfferIndex ||
                !selectedOffer.hasOrderReward ||
                !selectedOffer.hasExpectedProfit)
            {
                throw new InvalidOperationException(
                    $"Selected customer order {visit.EntityId} owns an invalid offer.");
            }

            _consultationLineEntityBuffer.Clear();
            foreach (GameEntity line in
                     _gameContext.GetEntitiesWithConsultationOfferEntityId(
                         selectedOffer.EntityId))
            {
                if (!line.isDestructed)
                    _consultationLineEntityBuffer.Add(line);
            }
            _consultationLineEntityBuffer.Sort(OrderLineComparer.Instance);
            ValidateConsultationLines(
                visit,
                selectedOffer,
                storageZone,
                _consultationLineEntityBuffer);
            for (int index = 0;
                 index < _consultationLineEntityBuffer.Count;
                 index++)
            {
                GameEntity line = _consultationLineEntityBuffer[index];
                _exactDemandLineBuffer.Add(new ExactDemandLine(
                    line.EntityId,
                    line.ProductType,
                    line.RequiredProductCount,
                    line.AvailableProductCount));
            }
        }

        private void CollectConfirmedOrderLines(
            GameEntity visit,
            GameEntity store,
            GameEntity storageZone,
            CustomerProjectTypeId projectType)
        {
            ValidateOrder(visit, store, storageZone, projectType);
            _orderLineEntityBuffer.Clear();
            foreach (GameEntity line in
                     _gameContext.GetEntitiesWithOrderEntityId(visit.EntityId))
            {
                if (!line.hasLineIndex)
                {
                    throw new InvalidOperationException(
                        $"Order {visit.EntityId} contains a line without an index.");
                }
                _orderLineEntityBuffer.Add(line);
            }

            _orderLineEntityBuffer.Sort(OrderLineComparer.Instance);
            ValidateOrderLines(visit, storageZone, _orderLineEntityBuffer);
            for (int index = 0; index < _orderLineEntityBuffer.Count; index++)
            {
                GameEntity line = _orderLineEntityBuffer[index];
                _exactDemandLineBuffer.Add(new ExactDemandLine(
                    line.EntityId,
                    line.ProductType,
                    checked(line.RequiredProductCount - line.LoadedProductCount),
                    line.AvailableProductCount));
            }
        }

        private static void ValidateSelectedCustomerOrder(
            GameEntity visit,
            GameEntity store,
            GameEntity storageZone,
            CustomerProjectTypeId projectType)
        {
            if (visit == null || !visit.isCustomerVisit || visit.isDestructed ||
                visit.isOrder || visit.isOrderRewarded || !visit.hasEntityId ||
                !visit.hasCustomerVisitStoreEntityId ||
                !visit.hasCustomerProjectType || !visit.hasStorageZoneEntityId ||
                !visit.hasCustomerArrivalSequence ||
                visit.CustomerVisitStoreEntityId != store.EntityId ||
                visit.CustomerProjectType != projectType ||
                visit.StorageZoneEntityId != storageZone.EntityId ||
                (!visit.isCustomerVisitArriving &&
                 !visit.isCustomerVisitQueued &&
                 !visit.isCustomerVisitConsulting))
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} cannot present procurement without an " +
                    "active selected customer order.");
            }

            ValidateVisitLifecycle(visit);
        }

        private static void ValidateOrder(
            GameEntity visit,
            GameEntity store,
            GameEntity storageZone,
            CustomerProjectTypeId projectType)
        {
            if (visit == null || !visit.isCustomerVisit || visit.isDestructed ||
                !visit.isOrder || visit.isOrderRewarded || !visit.hasEntityId ||
                !visit.hasCustomerVisitStoreEntityId ||
                !visit.hasCustomerProjectType || !visit.hasStorageZoneEntityId ||
                !visit.hasCustomerArrivalSequence ||
                visit.CustomerVisitStoreEntityId != store.EntityId ||
                visit.CustomerProjectType != projectType ||
                visit.StorageZoneEntityId != storageZone.EntityId)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} cannot present procurement without an active " +
                    "customer order.");
            }

            ValidateVisitLifecycle(visit);
        }

        private static void ValidateVisitLifecycle(GameEntity visit)
        {
            int lifecycleCount = 0;
            if (visit.isCustomerVisitArriving) lifecycleCount++;
            if (visit.isCustomerVisitQueued) lifecycleCount++;
            if (visit.isCustomerVisitConsulting) lifecycleCount++;
            if (visit.isCustomerVisitWaitingForLoadingBay) lifecycleCount++;
            if (visit.isCustomerVisitMovingToLoadingBay) lifecycleCount++;
            if (visit.isCustomerVisitLoading) lifecycleCount++;
            if (visit.isCustomerVisitCompleted) lifecycleCount++;
            if (visit.isCustomerVisitReturning) lifecycleCount++;
            if (visit.isCustomerVisitDeparting) lifecycleCount++;
            if (visit.isCustomerVisitAbandoning) lifecycleCount++;
            if (visit.isCustomerVisitWaitingForAbandonDeparture) lifecycleCount++;
            if (visit.isCustomerVisitAbandonDeparting) lifecycleCount++;
            if (lifecycleCount != 1)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} must have exactly one lifecycle marker.");
            }
        }

        private static void ValidateOrderLines(
            GameEntity visit,
            GameEntity storageZone,
            IReadOnlyList<GameEntity> lines)
        {
            if (lines.Count == 0 ||
                lines.Count > CustomerProjectConfig.MaxLinesPerOffer)
            {
                throw new InvalidOperationException(
                    $"Order {visit.EntityId} must expose between one and " +
                    $"{CustomerProjectConfig.MaxLinesPerOffer} product lines, found " +
                    $"{lines.Count}.");
            }

            for (int index = 0; index < lines.Count; index++)
            {
                GameEntity line = lines[index];
                if (!line.isOrderLine || line.isDestructed ||
                    !line.hasEntityId || !line.hasOrderEntityId ||
                    !line.hasStorageZoneEntityId || !line.hasLineIndex ||
                    !line.hasProductType || !line.hasRequiredProductCount ||
                    !line.hasAvailableProductCount || !line.hasLoadedProductCount ||
                    line.OrderEntityId != visit.EntityId ||
                    line.StorageZoneEntityId != storageZone.EntityId ||
                    line.LineIndex != index || line.RequiredProductCount <= 0 ||
                    line.AvailableProductCount < 0 || line.LoadedProductCount < 0 ||
                    line.LoadedProductCount > line.RequiredProductCount)
                {
                    throw new InvalidOperationException(
                        $"Order {visit.EntityId} has an invalid line at position {index}.");
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (lines[previous].ProductType == line.ProductType)
                    {
                        throw new InvalidOperationException(
                            $"Order {visit.EntityId} contains duplicate product type " +
                            $"{line.ProductType}.");
                    }
                }
            }
        }

        private static void ValidateConsultationLines(
            GameEntity visit,
            GameEntity offer,
            GameEntity storageZone,
            IReadOnlyList<GameEntity> lines)
        {
            if (lines.Count == 0 ||
                lines.Count > CustomerProjectConfig.MaxLinesPerOffer)
            {
                throw new InvalidOperationException(
                    $"Selected customer order {visit.EntityId} must expose between one " +
                    $"and {CustomerProjectConfig.MaxLinesPerOffer} product lines, found " +
                    $"{lines.Count}.");
            }

            for (int index = 0; index < lines.Count; index++)
            {
                GameEntity line = lines[index];
                if (!line.isConsultationOfferLine || line.isDestructed ||
                    !line.hasEntityId || !line.hasConsultationOfferEntityId ||
                    !line.hasStorageZoneEntityId || !line.hasLineIndex ||
                    !line.hasProductType || !line.hasRequiredProductCount ||
                    !line.hasAvailableProductCount ||
                    line.ConsultationOfferEntityId != offer.EntityId ||
                    line.StorageZoneEntityId != storageZone.EntityId ||
                    line.LineIndex != index || line.RequiredProductCount <= 0 ||
                    line.AvailableProductCount < 0)
                {
                    throw new InvalidOperationException(
                        $"Selected customer order {visit.EntityId} has an invalid " +
                        $"line at position {index}.");
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (lines[previous].ProductType == line.ProductType)
                    {
                        throw new InvalidOperationException(
                            $"Selected customer order {visit.EntityId} contains duplicate " +
                            $"product type {line.ProductType}.");
                    }
                }
            }
        }

        private readonly struct ExactDemandLine
        {
            public ExactDemandLine(
                int entityId,
                ProductTypeId productType,
                int remainingRequiredProductCount,
                int availableProductCount)
            {
                if (entityId <= 0)
                    throw new ArgumentOutOfRangeException(nameof(entityId));
                if (!Enum.IsDefined(typeof(ProductTypeId), productType))
                    throw new ArgumentOutOfRangeException(nameof(productType));
                if (remainingRequiredProductCount < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(remainingRequiredProductCount));
                }
                if (availableProductCount < 0)
                    throw new ArgumentOutOfRangeException(nameof(availableProductCount));

                EntityId = entityId;
                ProductType = productType;
                RemainingRequiredProductCount = remainingRequiredProductCount;
                AvailableProductCount = availableProductCount;
            }

            public int EntityId { get; }
            public ProductTypeId ProductType { get; }
            public int RemainingRequiredProductCount { get; }
            public int AvailableProductCount { get; }
        }

        private sealed class CartLineComparer : IComparer<GameEntity>
        {
            public static readonly CartLineComparer Instance = new();

            public int Compare(GameEntity left, GameEntity right)
            {
                int result = CompareOptionalProductType(left, right);
                return result != 0 ? result : CompareEntityId(left, right);
            }
        }

        private sealed class OrderLineComparer : IComparer<GameEntity>
        {
            public static readonly OrderLineComparer Instance = new();

            public int Compare(GameEntity left, GameEntity right)
            {
                int result = CompareOptionalInt(
                    left.hasLineIndex,
                    left.hasLineIndex ? left.LineIndex : 0,
                    right.hasLineIndex,
                    right.hasLineIndex ? right.LineIndex : 0);
                if (result != 0)
                    return result;

                result = CompareOptionalProductType(left, right);
                return result != 0 ? result : CompareEntityId(left, right);
            }
        }

        private sealed class ConsultationOfferComparer : IComparer<GameEntity>
        {
            public static readonly ConsultationOfferComparer Instance = new();

            public int Compare(GameEntity left, GameEntity right)
            {
                int result = CompareOptionalInt(
                    left.hasOfferIndex,
                    left.hasOfferIndex ? left.OfferIndex : 0,
                    right.hasOfferIndex,
                    right.hasOfferIndex ? right.OfferIndex : 0);
                return result != 0 ? result : CompareEntityId(left, right);
            }
        }

        private sealed class PurchaseOrderLineComparer : IComparer<GameEntity>
        {
            public static readonly PurchaseOrderLineComparer Instance = new();

            public int Compare(GameEntity left, GameEntity right)
            {
                int result = CompareOptionalInt(
                    left.hasPurchaseOrderLineIndex,
                    left.hasPurchaseOrderLineIndex
                        ? left.PurchaseOrderLineIndex
                        : 0,
                    right.hasPurchaseOrderLineIndex,
                    right.hasPurchaseOrderLineIndex
                        ? right.PurchaseOrderLineIndex
                        : 0);
                if (result != 0)
                    return result;

                result = CompareOptionalProductType(left, right);
                return result != 0 ? result : CompareEntityId(left, right);
            }
        }

        private sealed class CustomerVisitComparer : IComparer<GameEntity>
        {
            public static readonly CustomerVisitComparer Instance = new();

            public int Compare(GameEntity left, GameEntity right)
            {
                int result = CompareOptionalInt(
                    left.hasCustomerArrivalSequence,
                    left.hasCustomerArrivalSequence
                        ? left.CustomerArrivalSequence
                        : 0,
                    right.hasCustomerArrivalSequence,
                    right.hasCustomerArrivalSequence
                        ? right.CustomerArrivalSequence
                        : 0);
                return result != 0 ? result : CompareEntityId(left, right);
            }
        }

        private static int CompareOptionalProductType(
            GameEntity left,
            GameEntity right) =>
            CompareOptionalInt(
                left.hasProductType,
                left.hasProductType ? (int)left.ProductType : 0,
                right.hasProductType,
                right.hasProductType ? (int)right.ProductType : 0);

        private static int CompareEntityId(GameEntity left, GameEntity right) =>
            CompareOptionalInt(
                left.hasEntityId,
                left.hasEntityId ? left.EntityId : 0,
                right.hasEntityId,
                right.hasEntityId ? right.EntityId : 0);

        private static int CompareOptionalInt(
            bool hasLeft,
            int left,
            bool hasRight,
            int right)
        {
            if (hasLeft != hasRight)
                return hasLeft ? 1 : -1;
            return hasLeft ? left.CompareTo(right) : 0;
        }
    }
}
