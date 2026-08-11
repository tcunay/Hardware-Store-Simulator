using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
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

        public GameEntity CreateOrderCounter(int storeEntityId)
        {
            return CreateEntity.Empty(_identifiers.Next())
                .AddStoreEntityId(storeEntityId)
                .AddSceneViewKey(SceneViewId.CustomerOrderCounter)
                .With(x => x.isOrderCounter = true)
                .With(x => x.isInteractable = true);
        }

        public GameEntity CreateProcurementTerminal(int storeEntityId, int storageZoneEntityId,
            Pose deliveryPose)
        {
            return CreateEntity.Empty(_identifiers.Next())
                .AddStoreEntityId(storeEntityId)
                .AddStorageZoneEntityId(storageZoneEntityId)
                .AddSceneViewKey(SceneViewId.ProcurementTerminal)
                .AddSelectedProductType(_staticData.ProductTypes[0])
                .AddDeliverySpawnPosition(deliveryPose.position)
                .AddDeliverySpawnRotation(deliveryPose.rotation)
                .With(x => x.isProcurementTerminal = true)
                .With(x => x.isInteractable = true);
        }

        public GameEntity CreateStorageZone()
        {
            return CreateEntity.Empty(_identifiers.Next())
                .AddSceneViewKey(SceneViewId.StorageZone)
                .AddOccupiedStorageSlotCount(0)
                .AddStorageProductCount(0)
                .With(x => x.isStorageZone = true)
                .With(x => x.isInteractable = true);
        }

        public GameEntity CreateTrolleyUpgradeTerminal(
            int storeEntityId,
            Pose trolleySpawnPose)
        {
            return CreateEntity.Empty(_identifiers.Next())
                .AddStoreEntityId(storeEntityId)
                .AddSceneViewKey(SceneViewId.TrolleyUpgradeTerminal)
                .AddTrolleySpawnPosition(trolleySpawnPose.position)
                .AddTrolleySpawnRotation(trolleySpawnPose.rotation)
                .With(x => x.isTrolleyUpgradeTerminal = true)
                .With(x => x.isInteractable = true);
        }
    }
}
