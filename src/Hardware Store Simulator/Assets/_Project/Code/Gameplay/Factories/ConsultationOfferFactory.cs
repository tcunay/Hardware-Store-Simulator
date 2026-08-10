using System;
using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class ConsultationOfferFactory : IConsultationOfferFactory
    {
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;

        public ConsultationOfferFactory(IIdentifierService identifiers,
            IStaticDataService staticData)
        {
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public void CreateOffers(GameEntity customerVisit)
        {
            ValidateCustomerVisit(customerVisit);

            OrderConfig order = _staticData.GetOrder(customerVisit.RequestedProductType);
            DeliveryConfig delivery = _staticData.GetDelivery(customerVisit.RequestedProductType);
            for (int index = 0; index < order.Offers.Count; index++)
            {
                OrderOfferDefinition definition = order.Offers[index];
                int expectedProfit = checked(
                    definition.Reward -
                    delivery.PurchaseUnitPrice * definition.RequiredProductCount);

                CreateEntity.Empty(_identifiers.Next())
                    .AddCustomerVisitEntityId(customerVisit.EntityId)
                    .AddStorageZoneEntityId(customerVisit.StorageZoneEntityId)
                    .AddOfferIndex(index)
                    .AddOfferTitle(definition.Title)
                    .AddOfferDescription(definition.Description)
                    .AddRequiredProductType(order.RequiredProductType)
                    .AddRequiredProductCount(definition.RequiredProductCount)
                    .AddAvailableProductCount(0)
                    .AddOrderReward(definition.Reward)
                    .AddExpectedProfit(expectedProfit)
                    .With(x => x.isConsultationOffer = true)
                    .With(x => x.isSelectedConsultationOffer =
                        index == order.DefaultOfferIndex);
            }
        }

        private static void ValidateCustomerVisit(GameEntity customerVisit)
        {
            if (customerVisit == null)
                throw new ArgumentNullException(nameof(customerVisit));
            if (!customerVisit.isCustomerVisit || !customerVisit.hasEntityId ||
                !customerVisit.hasRequestedProductType ||
                !customerVisit.hasStorageZoneEntityId || customerVisit.isOrder)
            {
                throw new InvalidOperationException(
                    "Consultation offers require a fresh configured customer visit.");
            }
        }
    }
}
