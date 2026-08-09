using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class DeliveryFactory : IDeliveryFactory
    {
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;

        public DeliveryFactory(IIdentifierService identifiers, IStaticDataService staticData)
        {
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public GameEntity Create(int procurementTerminalEntityId, int storeEntityId, Pose at)
        {
            DeliveryConfig config = _staticData.Delivery;
            return CreateEntity.Empty(_identifiers.Next())
                .AddViewPrefab(config.ViewPrefab)
                .AddSpawnPosition(at.position)
                .AddSpawnRotation(at.rotation)
                .AddProductType(config.ProductType)
                .AddDeliveryProductCount(config.ProductCount)
                .AddStockedProductCount(0)
                .AddDeliveryCost(config.TotalCost)
                .AddProcurementTerminalEntityId(procurementTerminalEntityId)
                .AddStoreEntityId(storeEntityId)
                .With(x => x.isDelivery = true)
                .With(x => x.isDeliveryActive = true);
        }
    }
}
