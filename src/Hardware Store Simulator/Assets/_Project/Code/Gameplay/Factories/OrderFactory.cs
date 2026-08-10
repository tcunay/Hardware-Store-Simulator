using System;
using HardwareStore.Common.Extensions;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class OrderFactory : IOrderFactory
    {
        public GameEntity AddOrderComponents(GameEntity customerVisit,
            GameEntity selectedOffer)
        {
            if (customerVisit == null)
                throw new ArgumentNullException(nameof(customerVisit));
            if (selectedOffer == null)
                throw new ArgumentNullException(nameof(selectedOffer));
            if (!customerVisit.isCustomerVisit ||
                !customerVisit.isCustomerVisitConsulting ||
                !customerVisit.hasEntityId ||
                !customerVisit.hasRequestedProductType ||
                !customerVisit.hasStorageZoneEntityId || customerVisit.isOrder)
            {
                throw new InvalidOperationException(
                    "Order components require a consulting customer visit.");
            }
            if (!selectedOffer.isConsultationOffer ||
                !selectedOffer.isSelectedConsultationOffer ||
                !selectedOffer.hasCustomerVisitEntityId ||
                !selectedOffer.hasStorageZoneEntityId ||
                !selectedOffer.hasRequiredProductType ||
                !selectedOffer.hasRequiredProductCount ||
                !selectedOffer.hasAvailableProductCount ||
                !selectedOffer.hasOrderReward ||
                !selectedOffer.hasExpectedProfit)
            {
                throw new InvalidOperationException(
                    "Order components require a complete selected consultation offer.");
            }
            if (selectedOffer.CustomerVisitEntityId != customerVisit.EntityId ||
                selectedOffer.StorageZoneEntityId != customerVisit.StorageZoneEntityId ||
                selectedOffer.RequiredProductType != customerVisit.RequestedProductType)
            {
                throw new InvalidOperationException(
                    "The selected consultation offer does not belong to the customer visit.");
            }

            customerVisit
                .AddRequiredProductType(selectedOffer.RequiredProductType)
                .AddRequiredProductCount(selectedOffer.RequiredProductCount)
                .AddAvailableProductCount(selectedOffer.AvailableProductCount)
                .AddLoadedProductCount(0)
                .AddOrderReward(selectedOffer.OrderReward)
                .AddExpectedProfit(selectedOffer.ExpectedProfit)
                .With(x => x.isOrder = true);
            return customerVisit;
        }
    }
}
