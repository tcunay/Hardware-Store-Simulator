using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class InteractionTargetFactory : IInteractionTargetFactory
    {
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;

        public InteractionTargetFactory(IIdentifierService identifiers, IStaticDataService staticData)
        {
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public GameEntity CreateOrderCounter(EntityBehaviour view, int orderEntityId)
        {
            int entityId = _identifiers.Next();
            GameEntity entity = CreateEntity.Empty(entityId)
                .AddOrderEntityId(orderEntityId)
                .With(x => x.isOrderCounter = true)
                .With(x => x.isInteractable = true);

            view.SetEntity(entity);
            return entity;
        }

        public GameEntity CreateCustomerLoadingZone(EntityBehaviour view, int orderEntityId)
        {
            int entityId = _identifiers.Next();
            GameEntity entity = CreateEntity.Empty(entityId)
                .AddOrderEntityId(orderEntityId)
                .With(x => x.isLoadingZone = true)
                .With(x => x.isInteractable = true);

            view.SetEntity(entity);
            return entity;
        }

        public GameEntity CreateProcurementTerminal(EntityBehaviour view, int storeEntityId,
            int storageZoneEntityId, Pose deliveryPose)
        {
            DeliveryConfig config = _staticData.Delivery;
            GameEntity entity = CreateEntity.Empty(_identifiers.Next())
                .AddStoreEntityId(storeEntityId)
                .AddStorageZoneEntityId(storageZoneEntityId)
                .AddProductType(config.ProductType)
                .AddDeliveryProductCount(config.ProductCount)
                .AddDeliveryCost(config.TotalCost)
                .AddSpawnPosition(deliveryPose.position)
                .AddSpawnRotation(deliveryPose.rotation)
                .With(x => x.isProcurementTerminal = true)
                .With(x => x.isInteractable = true);

            view.SetEntity(entity);
            return entity;
        }

        public GameEntity CreateStorageZone(EntityBehaviour view)
        {
            GameEntity entity = CreateEntity.Empty(_identifiers.Next())
                .AddOccupiedStorageSlotCount(0)
                .With(x => x.isStorageZone = true)
                .With(x => x.isInteractable = true);

            view.SetEntity(entity);
            return entity;
        }
    }
}
