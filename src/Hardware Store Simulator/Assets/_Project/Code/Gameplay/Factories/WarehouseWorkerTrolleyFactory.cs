using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class WarehouseWorkerTrolleyFactory :
        IWarehouseWorkerTrolleyFactory
    {
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;

        public WarehouseWorkerTrolleyFactory(IIdentifierService identifiers,
            IStaticDataService staticData)
        {
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public GameEntity Create(int storeEntityId, Pose homePose,
            Pose customerLoadingPose)
        {
            WarehouseWorkerConfig config = _staticData.WarehouseWorker;
            return CreateEntity.Empty(_identifiers.Next())
                .AddViewPrefab(config.TrolleyViewPrefab)
                .AddSpawnPosition(homePose.position)
                .AddSpawnRotation(homePose.rotation)
                .AddWorkerTrolleyStoreEntityId(storeEntityId)
                .AddTrolleyCapacity(config.TrolleyCapacity)
                .AddOccupiedTrolleySlotCount(0)
                .AddTrolleyFollowDistance(config.TrolleyFollowDistance)
                .AddWorkerTrolleyHomePosition(homePose.position)
                .AddWorkerTrolleyHomeRotation(homePose.rotation)
                .AddWorkerTrolleyCustomerLoadingPosition(
                    customerLoadingPose.position)
                .AddWorkerTrolleyCustomerLoadingRotation(
                    customerLoadingPose.rotation)
                .With(x => x.isWorkerTrolley = true);
        }
    }
}
