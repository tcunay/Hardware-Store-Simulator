using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class OrderFactory : IOrderFactory
    {
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;

        public OrderFactory(IIdentifierService identifiers, IStaticDataService staticData)
        {
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public GameEntity CreateOrder(int storeEntityId, int storageZoneEntityId)
        {
            OrderConfig config = _staticData.Order;
            return CreateEntity.Empty(_identifiers.Next())
                .AddStoreEntityId(storeEntityId)
                .AddStorageZoneEntityId(storageZoneEntityId)
                .AddRequiredProductType(config.RequiredProductType)
                .AddRequiredProductCount(config.RequiredProductCount)
                .AddAvailableProductCount(0)
                .AddLoadedProductCount(0)
                .AddOrderReward(config.Reward)
                .With(x => x.isOrder = true)
                .With(x => x.isOrderWaiting = true);
        }
    }
}
