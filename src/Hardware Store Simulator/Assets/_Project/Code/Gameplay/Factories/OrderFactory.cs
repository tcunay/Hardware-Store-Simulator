using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Infrastructure.Identifiers;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class OrderFactory : IOrderFactory
    {
        private readonly GameContext _gameContext;
        private readonly IIdentifierService _identifiers;
        private readonly List<GameEntity> _lineBuffer = new(4);
        private readonly HashSet<HardwareStore.Gameplay.Components.ProductTypeId> _productTypes =
            new();

        public OrderFactory(GameContext gameContext, IIdentifierService identifiers)
        {
            _gameContext = gameContext;
            _identifiers = identifiers;
        }

        public GameEntity AddOrderComponents(GameEntity customerVisit,
            GameEntity selectedOffer)
        {
            ValidateCustomerVisit(customerVisit);
            ValidateSelectedOffer(customerVisit, selectedOffer);

            _lineBuffer.Clear();
            _productTypes.Clear();
            foreach (GameEntity entity in
                     _gameContext.GetEntitiesWithConsultationOfferEntityId(
                         selectedOffer.EntityId))
            {
                ValidateOfferLine(selectedOffer, customerVisit, entity);
                if (!_productTypes.Add(entity.ProductType))
                    throw new InvalidOperationException(
                        $"Selected consultation offer {selectedOffer.EntityId} contains " +
                        $"duplicate product type {entity.ProductType}.");
                _lineBuffer.Add(entity);
            }

            if (_lineBuffer.Count == 0)
                throw new InvalidOperationException(
                    $"Selected consultation offer {selectedOffer.EntityId} has no lines.");

            _lineBuffer.Sort((left, right) => left.LineIndex.CompareTo(right.LineIndex));
            for (int lineIndex = 0; lineIndex < _lineBuffer.Count; lineIndex++)
            {
                GameEntity offerLine = _lineBuffer[lineIndex];
                if (offerLine.LineIndex != lineIndex)
                    throw new InvalidOperationException(
                        $"Selected consultation offer {selectedOffer.EntityId} must use " +
                        "contiguous line indices starting at zero.");

                CreateEntity.Empty(_identifiers.Next())
                    .AddOrderEntityId(customerVisit.EntityId)
                    .AddStorageZoneEntityId(customerVisit.StorageZoneEntityId)
                    .AddLineIndex(lineIndex)
                    .AddProductType(offerLine.ProductType)
                    .AddRequiredProductCount(offerLine.RequiredProductCount)
                    .AddAvailableProductCount(offerLine.AvailableProductCount)
                    .AddLoadedProductCount(0)
                    .With(x => x.isOrderLine = true);
            }

            customerVisit
                .AddOrderReward(selectedOffer.OrderReward)
                .AddExpectedProfit(selectedOffer.ExpectedProfit)
                .With(x => x.isOrder = true);
            return customerVisit;
        }

        private void ValidateCustomerVisit(GameEntity customerVisit)
        {
            if (customerVisit == null)
                throw new ArgumentNullException(nameof(customerVisit));
            if (!customerVisit.isCustomerVisit ||
                !customerVisit.isCustomerVisitConsulting ||
                !customerVisit.hasEntityId ||
                !customerVisit.hasCustomerProjectType ||
                !customerVisit.hasStorageZoneEntityId || customerVisit.isOrder)
            {
                throw new InvalidOperationException(
                    "Order components require a consulting customer visit.");
            }

            foreach (GameEntity existingLine in
                     _gameContext.GetEntitiesWithOrderEntityId(customerVisit.EntityId))
            {
                if (!existingLine.isDestructed)
                    throw new InvalidOperationException(
                        $"Customer visit {customerVisit.EntityId} already has an order line.");
            }
        }

        private static void ValidateSelectedOffer(GameEntity customerVisit,
            GameEntity selectedOffer)
        {
            if (selectedOffer == null)
                throw new ArgumentNullException(nameof(selectedOffer));
            if (!selectedOffer.isConsultationOffer ||
                !selectedOffer.isSelectedConsultationOffer ||
                !selectedOffer.hasEntityId ||
                !selectedOffer.hasConsultationOfferVisitEntityId ||
                !selectedOffer.hasOfferIndex ||
                !selectedOffer.hasOrderReward ||
                !selectedOffer.hasExpectedProfit)
            {
                throw new InvalidOperationException(
                    "Order components require a complete selected consultation offer.");
            }
            if (selectedOffer.ConsultationOfferVisitEntityId != customerVisit.EntityId)
                throw new InvalidOperationException(
                    "The selected consultation offer does not belong to the customer visit.");
        }

        private static void ValidateOfferLine(GameEntity selectedOffer,
            GameEntity customerVisit, GameEntity line)
        {
            if (!line.isConsultationOfferLine || line.isDestructed ||
                !line.hasEntityId || !line.hasConsultationOfferEntityId ||
                !line.hasStorageZoneEntityId || !line.hasLineIndex ||
                !line.hasProductType || !line.hasRequiredProductCount ||
                !line.hasAvailableProductCount)
            {
                throw new InvalidOperationException(
                    $"Consultation offer {selectedOffer.EntityId} has an invalid line entity.");
            }
            if (line.ConsultationOfferEntityId != selectedOffer.EntityId ||
                line.StorageZoneEntityId != customerVisit.StorageZoneEntityId)
            {
                throw new InvalidOperationException(
                    $"Consultation offer line {line.EntityId} has invalid ownership.");
            }
        }
    }
}
