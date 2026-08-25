using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class RefreshCustomerDemandSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IStaticDataService _staticData;
        private readonly IGroup<GameEntity> _stores;
        private readonly IGroup<GameEntity> _ownedProducts;
        private readonly IGroup<GameEntity> _shelfProducts;
        private readonly IGroup<GameEntity> _consultationLines;
        private readonly IGroup<GameEntity> _orderLines;
        private readonly Dictionary<StockKey, int> _freeProductCounts = new(32);
        private readonly Dictionary<StockKey, int> _shelfProductCounts = new(32);
        private readonly List<GameEntity> _storeBuffer = new(2);

        public RefreshCustomerDemandSystem(
            GameContext gameContext,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _staticData = staticData;
            _stores = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Store,
                    GameMatcher.EntityId,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.NextProjectSequenceIndex,
                    GameMatcher.NextCustomerArrivalSequence)
                .NoneOf(GameMatcher.Destructed));
            _ownedProducts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.EntityId,
                    GameMatcher.InStock,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.ProductType)
                .NoneOf(GameMatcher.Destructed));
            _shelfProducts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.EntityId,
                    GameMatcher.InStock,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.StorageSlotIndex,
                    GameMatcher.ProductType,
                    GameMatcher.Interactable)
                .NoneOf(GameMatcher.Destructed));
            _consultationLines = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.ConsultationOfferLine,
                    GameMatcher.EntityId,
                    GameMatcher.ConsultationOfferEntityId,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.ProductType,
                    GameMatcher.RequiredProductCount)
                .NoneOf(GameMatcher.Destructed));
            _orderLines = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.OrderLine,
                    GameMatcher.EntityId,
                    GameMatcher.OrderEntityId,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.ProductType,
                    GameMatcher.RequiredProductCount,
                    GameMatcher.LoadedProductCount)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            BuildFreeProductCounts();

            foreach (GameEntity store in _stores.GetEntities(_storeBuffer))
            {
                ValidateStore(store);
                if (TrySelectDemand(
                        store,
                        out CustomerProjectTypeId projectType,
                        out int offerIndex))
                {
                    SetAvailableDemand(store, projectType, offerIndex);
                }
                else
                {
                    SetUnavailableDemand(store);
                }
            }
        }

        private void BuildFreeProductCounts()
        {
            _freeProductCounts.Clear();
            _shelfProductCounts.Clear();
            foreach (GameEntity product in _ownedProducts)
            {
                ValidateOwnedProduct(product);
                AddProducts(
                    _freeProductCounts,
                    new StockKey(product.StorageZoneEntityId, product.ProductType),
                    1);
            }

            foreach (GameEntity product in _shelfProducts)
            {
                ValidateShelfProduct(product);
                AddProducts(
                    _shelfProductCounts,
                    new StockKey(product.StorageZoneEntityId, product.ProductType),
                    1);
            }

            foreach (GameEntity line in _consultationLines)
            {
                ValidateConsultationReservation(line);
                ReserveProducts(
                    line.EntityId,
                    new StockKey(line.StorageZoneEntityId, line.ProductType),
                    line.RequiredProductCount);
            }

            foreach (GameEntity line in _orderLines)
            {
                ValidateOrderReservation(line);
                int remainingProductCount = checked(
                    line.RequiredProductCount - line.LoadedProductCount);
                ReserveProducts(
                    line.EntityId,
                    new StockKey(line.StorageZoneEntityId, line.ProductType),
                    remainingProductCount);
            }
        }

        private bool TrySelectDemand(
            GameEntity store,
            out CustomerProjectTypeId projectType,
            out int offerIndex)
        {
            IReadOnlyList<CustomerProjectTypeId> projectTypes =
                _staticData.ProjectTypes;
            for (int projectOffset = 0;
                 projectOffset < projectTypes.Count;
                 projectOffset++)
            {
                int projectIndex =
                    (store.NextProjectSequenceIndex + projectOffset) %
                    projectTypes.Count;
                CustomerProjectConfig project = _staticData.GetProject(
                    projectTypes[projectIndex]);
                if (TrySelectOffer(
                        store,
                        project,
                        out offerIndex))
                {
                    projectType = project.ProjectType;
                    return true;
                }
            }

            projectType = default;
            offerIndex = -1;
            return false;
        }

        private bool TrySelectOffer(
            GameEntity store,
            CustomerProjectConfig project,
            out int offerIndex)
        {
            offerIndex = -1;
            int selectedUnitCount = -1;
            for (int index = 0; index < project.Offers.Count; index++)
            {
                CustomerProjectOfferDefinition offer = project.Offers[index];
                if (!CanFulfill(store.StorageZoneEntityId, offer))
                    continue;

                int unitCount = 0;
                foreach (CustomerProjectLineDefinition line in offer.Lines)
                    unitCount = checked(unitCount + line.RequiredCount);
                if (unitCount <= selectedUnitCount)
                    continue;

                offerIndex = index;
                selectedUnitCount = unitCount;
            }

            return offerIndex >= 0;
        }

        private bool CanFulfill(
            int storageZoneEntityId,
            CustomerProjectOfferDefinition offer)
        {
            foreach (CustomerProjectLineDefinition line in offer.Lines)
            {
                _freeProductCounts.TryGetValue(
                    new StockKey(storageZoneEntityId, line.ProductType),
                    out int freeOwnedCount);
                _shelfProductCounts.TryGetValue(
                    new StockKey(storageZoneEntityId, line.ProductType),
                    out int shelfCount);
                if (freeOwnedCount < line.RequiredCount ||
                    shelfCount < line.RequiredCount)
                    return false;
            }

            return true;
        }

        private void ValidateConsultationReservation(GameEntity line)
        {
            if (line.RequiredProductCount <= 0)
            {
                throw new InvalidOperationException(
                    $"Consultation line {line.EntityId} has invalid demand " +
                    $"{line.RequiredProductCount}.");
            }

            GameEntity offer = _gameContext.GetEntityWithEntityId(
                line.ConsultationOfferEntityId);
            if (offer == null || offer.isDestructed ||
                !offer.isConsultationOffer ||
                !offer.isSelectedConsultationOffer ||
                !offer.hasEntityId ||
                !offer.hasConsultationOfferVisitEntityId ||
                offer.EntityId != line.ConsultationOfferEntityId)
            {
                throw new InvalidOperationException(
                    $"Consultation line {line.EntityId} has no selected demand offer.");
            }

            GameEntity visit = _gameContext.GetEntityWithEntityId(
                offer.ConsultationOfferVisitEntityId);
            if (visit == null || visit.isDestructed ||
                !visit.isCustomerVisit || visit.isOrder ||
                !visit.hasEntityId || !visit.hasStorageZoneEntityId ||
                visit.StorageZoneEntityId != line.StorageZoneEntityId ||
                (!visit.isCustomerVisitArriving &&
                 !visit.isCustomerVisitQueued &&
                 !visit.isCustomerVisitConsulting))
            {
                throw new InvalidOperationException(
                    $"Consultation line {line.EntityId} belongs to an inactive " +
                    "customer demand.");
            }
        }

        private void ValidateOrderReservation(GameEntity line)
        {
            if (line.RequiredProductCount <= 0 ||
                line.LoadedProductCount < 0 ||
                line.LoadedProductCount > line.RequiredProductCount)
            {
                throw new InvalidOperationException(
                    $"Order line {line.EntityId} has invalid required/loaded counts.");
            }

            GameEntity visit = _gameContext.GetEntityWithEntityId(line.OrderEntityId);
            if (visit == null || visit.isDestructed ||
                !visit.isCustomerVisit || !visit.isOrder ||
                !visit.hasEntityId || !visit.hasStorageZoneEntityId ||
                visit.StorageZoneEntityId != line.StorageZoneEntityId)
            {
                throw new InvalidOperationException(
                    $"Order line {line.EntityId} belongs to an inactive customer order.");
            }

            int reservedProductCount = 0;
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithReservedOrderLineEntityId(line.EntityId))
            {
                if (product.isDestructed || !product.isProduct ||
                    !product.isInStock || !product.hasEntityId ||
                    !product.hasProductType ||
                    product.ProductType != line.ProductType ||
                    !product.hasStorageZoneEntityId ||
                    product.StorageZoneEntityId != line.StorageZoneEntityId ||
                    !product.hasReservedStorageSlotIndex ||
                    product.hasStorageSlotIndex)
                {
                    throw new InvalidOperationException(
                        $"Order line {line.EntityId} has an invalid physical product " +
                        "reservation.");
                }

                reservedProductCount = checked(reservedProductCount + 1);
            }

            if (line.LoadedProductCount + reservedProductCount >
                line.RequiredProductCount)
            {
                throw new InvalidOperationException(
                    $"Order line {line.EntityId} over-reserves its required quantity.");
            }

        }

        private static void ValidateOwnedProduct(GameEntity product)
        {
            if (product.isInboundProduct || product.isLoaded ||
                product.hasDeliveryEntityId || product.hasDeliverySlotIndex ||
                product.hasReservedDeliverySlotIndex || product.hasOrderLineEntityId ||
                product.hasLoadingSlotIndex)
            {
                throw new InvalidOperationException(
                    $"Stock-owned product {product.EntityId} has conflicting lifecycle state.");
            }
        }

        private static void ValidateShelfProduct(GameEntity product)
        {
            if (product.isInboundProduct || product.isLoaded ||
                product.hasCarrierEntityId || product.hasReservedStorageSlotIndex ||
                product.hasReservedOrderLineEntityId ||
                product.hasOrderLineEntityId || product.hasLoadingSlotIndex ||
                product.hasDeliveryEntityId || product.hasDeliverySlotIndex ||
                product.hasReservedDeliverySlotIndex || product.isLooseProduct ||
                product.hasTrolleyEntityId || product.hasTrolleySlotIndex ||
                product.hasWorkerTrolleyEntityId ||
                product.hasWorkerTrolleySlotIndex)
            {
                throw new InvalidOperationException(
                    $"Shelf product {product.EntityId} has conflicting placement state.");
            }
        }

        private void ReserveProducts(int ownerEntityId, StockKey key, int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0)
                return;

            _freeProductCounts.TryGetValue(key, out int availableCount);
            if (availableCount < count)
            {
                throw new InvalidOperationException(
                    $"Demand entity {ownerEntityId} reserves {count} units of " +
                    $"{key.ProductType}, but storage zone {key.StorageZoneEntityId} " +
                    $"contains only {availableCount} free stock-owned units.");
            }

            _freeProductCounts[key] = availableCount - count;
        }

        private static void AddProducts(
            Dictionary<StockKey, int> productCounts,
            StockKey key,
            int count)
        {
            productCounts.TryGetValue(key, out int currentCount);
            productCounts[key] = checked(currentCount + count);
        }

        private void ValidateStore(GameEntity store)
        {
            if (store.NextProjectSequenceIndex < 0 ||
                store.NextProjectSequenceIndex >= _staticData.ProjectTypes.Count)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has invalid customer project cursor " +
                    $"{store.NextProjectSequenceIndex}.");
            }
            if (store.NextCustomerArrivalSequence < 0)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has invalid customer arrival sequence.");
            }
            if (store.hasCustomerDemandProjectType !=
                store.hasCustomerDemandOfferIndex)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an incomplete customer demand draft.");
            }
        }

        private void SetAvailableDemand(
            GameEntity store,
            CustomerProjectTypeId projectType,
            int offerIndex)
        {
            bool becameAvailable = store.isCustomerDemandUnavailable;
            if (store.hasCustomerDemandProjectType)
            {
                if (store.CustomerDemandProjectType != projectType)
                    store.ReplaceCustomerDemandProjectType(projectType);
                if (store.CustomerDemandOfferIndex != offerIndex)
                    store.ReplaceCustomerDemandOfferIndex(offerIndex);
            }
            else
            {
                store.AddCustomerDemandProjectType(projectType);
                store.AddCustomerDemandOfferIndex(offerIndex);
            }

            store.isCustomerDemandUnavailable = false;
            if (becameAvailable && store.isStoreOpen &&
                store.hasCustomerCooldownRemaining)
            {
                store.ReplaceCustomerCooldownRemaining(
                    _staticData.CustomerFlow.FirstArrivalDelay);
            }
        }

        private static void SetUnavailableDemand(GameEntity store)
        {
            if (store.hasCustomerDemandProjectType)
            {
                store.RemoveCustomerDemandProjectType();
                store.RemoveCustomerDemandOfferIndex();
            }

            store.isCustomerDemandUnavailable = true;
        }

        private readonly struct StockKey : IEquatable<StockKey>
        {
            public StockKey(
                int storageZoneEntityId,
                ProductTypeId productType)
            {
                StorageZoneEntityId = storageZoneEntityId;
                ProductType = productType;
            }

            public int StorageZoneEntityId { get; }
            public ProductTypeId ProductType { get; }

            public bool Equals(StockKey other) =>
                StorageZoneEntityId == other.StorageZoneEntityId &&
                ProductType == other.ProductType;

            public override bool Equals(object obj) =>
                obj is StockKey other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(StorageZoneEntityId, (int)ProductType);
        }
    }
}
