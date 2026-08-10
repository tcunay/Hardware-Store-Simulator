using System;
using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class OrderFactory : IOrderFactory
    {
        private readonly IStaticDataService _staticData;

        public OrderFactory(IStaticDataService staticData) => _staticData = staticData;

        public GameEntity AddOrderComponents(GameEntity customerVisit, int storageZoneEntityId,
            ProductTypeId productType)
        {
            if (customerVisit == null)
                throw new ArgumentNullException(nameof(customerVisit));
            if (!customerVisit.isCustomerVisit || !customerVisit.isCustomerVehicle ||
                !customerVisit.hasCustomerVisitStoreEntityId || customerVisit.isOrder)
            {
                throw new InvalidOperationException(
                    "Order components require a fresh configured customer visit.");
            }

            var config = _staticData.GetOrder(productType);
            return customerVisit
                .AddStorageZoneEntityId(storageZoneEntityId)
                .AddRequiredProductType(config.RequiredProductType)
                .AddRequiredProductCount(config.RequiredProductCount)
                .AddAvailableProductCount(0)
                .AddLoadedProductCount(0)
                .AddOrderReward(config.Reward)
                .With(x => x.isOrder = true);
        }
    }
}
