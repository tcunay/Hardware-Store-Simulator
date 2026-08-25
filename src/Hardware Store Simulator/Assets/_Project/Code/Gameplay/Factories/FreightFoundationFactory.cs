using System;
using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class FreightFoundationFactory : IFreightFoundationFactory
    {
        private const int InitialTruckPalletSlotIndex = 0;

        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;

        public FreightFoundationFactory(IIdentifierService identifiers,
            IStaticDataService staticData)
        {
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public void Create(Pose truckPose, Pose palletPose, int storeEntityId)
        {
            if (storeEntityId <= 0)
                throw new ArgumentOutOfRangeException(nameof(storeEntityId));

            GameEntity truck = CreateFreightTruck(truckPose, storeEntityId);
            CreateFreightStagingZone(storeEntityId);
            CreateInboundPallet(palletPose, storeEntityId, truck.EntityId);
        }

        private GameEntity CreateFreightTruck(Pose at, int storeEntityId)
        {
            FreightTruckConfig config = _staticData.FreightTruck;
            return CreateEntity.Empty(_identifiers.Next())
                .AddViewPrefab(config.ViewPrefab)
                .AddSpawnPosition(at.position)
                .AddSpawnRotation(at.rotation)
                .AddFreightTruckStoreEntityId(storeEntityId)
                .AddOccupiedPalletSlotCount(1)
                .With(x => x.isFreightTruck = true)
                .With(x => x.isPalletBay = true);
        }

        private GameEntity CreateFreightStagingZone(int storeEntityId) =>
            CreateEntity.Empty(_identifiers.Next())
                .AddSceneViewKey(SceneViewId.FreightStagingZone)
                .AddFreightStagingZoneStoreEntityId(storeEntityId)
                .AddOccupiedPalletSlotCount(0)
                .With(x => x.isFreightStagingZone = true)
                .With(x => x.isPalletBay = true);

        private GameEntity CreateInboundPallet(Pose at, int storeEntityId,
            int truckEntityId)
        {
            PalletConfig config = _staticData.Pallet;
            return CreateEntity.Empty(_identifiers.Next())
                .AddViewPrefab(config.ViewPrefab)
                .AddSpawnPosition(at.position)
                .AddSpawnRotation(at.rotation)
                .AddPalletStoreEntityId(storeEntityId)
                .AddPalletBayEntityId(truckEntityId)
                .AddPalletBaySlotIndex(InitialTruckPalletSlotIndex)
                .With(x => x.isPallet = true)
                .With(x => x.isInboundPallet = true);
        }
    }
}
