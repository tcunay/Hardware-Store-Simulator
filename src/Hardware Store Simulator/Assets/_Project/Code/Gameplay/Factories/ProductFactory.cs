using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class ProductFactory : IProductFactory
    {
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;

        public ProductFactory(IIdentifierService identifiers, IStaticDataService staticData)
        {
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public GameEntity CreateInbound(ProductTypeId productType, Pose at, int deliveryEntityId,
            int deliverySlotIndex)
        {
            ProductConfig config = _staticData.GetProduct(productType);
            return CreateEntity.Empty(_identifiers.Next())
                .AddViewPrefab(config.ViewPrefab)
                .AddSpawnPosition(at.position)
                .AddSpawnRotation(at.rotation)
                .AddDeliveryEntityId(deliveryEntityId)
                .AddDeliverySlotIndex(deliverySlotIndex)
                .AddProductType(config.ProductType)
                .AddUnitPrice(config.UnitPrice)
                .AddProductMass(config.Mass)
                .AddCarryMovementSpeed(config.CarryMovementSpeed)
                .AddHeldRotationOffset(config.HeldRotationOffset)
                .AddDropForwardDistance(config.DropForwardDistance)
                .AddProductDropCollisionRadius(config.ProductDropCollisionRadius)
                .AddRigidbodyInterpolationMode(config.WorldInterpolation)
                .AddRigidbodyCollisionDetectionMode(config.WorldCollisionDetection)
                .With(x => x.isProduct = true)
                .With(x => x.isInboundProduct = true)
                .With(x => x.isProductPlacementDirty = true)
                .With(x => x.isInteractable = true);
        }
    }
}
