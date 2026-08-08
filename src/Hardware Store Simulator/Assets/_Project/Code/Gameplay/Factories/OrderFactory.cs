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

        public GameEntity CreateOrder(int walletEntityId)
        {
            OrderConfig config = _staticData.Order;
            return CreateEntity.Empty(_identifiers.Next())
                .AddWalletEntityId(walletEntityId)
                .AddRequiredProductType(config.RequiredProductType)
                .AddRequiredProductCount(config.RequiredProductCount)
                .AddLoadedProductCount(0)
                .AddOrderReward(config.Reward)
                .With(x => x.isOrder = true)
                .With(x => x.isOrderWaiting = true);
        }

        public GameEntity CreateWallet() => CreateEntity.Empty(_identifiers.Next())
            .AddMoney(_staticData.Order.InitialMoney)
            .With(x => x.isWallet = true);
    }
}
