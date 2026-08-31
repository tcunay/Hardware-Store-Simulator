using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class WarehouseWorkerFactory : IWarehouseWorkerFactory
    {
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;

        public WarehouseWorkerFactory(IIdentifierService identifiers,
            IStaticDataService staticData)
        {
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public GameEntity Create(int storeEntityId, Pose spawnPose, Pose pickupPose,
            Pose storagePose, Pose customerLoadingPose)
        {
            WarehouseWorkerConfig config = _staticData.WarehouseWorker;
            return CreateEntity.Empty(_identifiers.Next())
                .AddViewPrefab(config.ViewPrefab)
                .AddSpawnPosition(spawnPose.position)
                .AddSpawnRotation(spawnPose.rotation)
                .AddWarehouseWorkerStoreEntityId(storeEntityId)
                .AddWarehouseWorkerStatus(WarehouseWorkerStatusId.Idle)
                .AddMovementSpeed(config.MovementSpeed)
                .AddTrafficControlPolicy(TrafficControlPolicyId.NavMesh)
                .AddTrafficPriority((int)TrafficPriorityId.WarehouseWorker)
                .AddTrafficDesiredVelocity(Vector3.zero)
                .AddTrafficIntentDistance(0f)
                .AddTrafficAngularIntent(0f)
                .AddTrafficPreviousPosition(spawnPose.position)
                .AddWarehouseWorkerPickupPosition(pickupPose.position)
                .AddWarehouseWorkerPickupRotation(pickupPose.rotation)
                .AddWarehouseWorkerStoragePosition(storagePose.position)
                .AddWarehouseWorkerStorageRotation(storagePose.rotation)
                .AddWarehouseWorkerCustomerLoadingPosition(
                    customerLoadingPose.position)
                .AddWarehouseWorkerCustomerLoadingRotation(
                    customerLoadingPose.rotation)
                .With(x => x.isWarehouseWorker = true)
                .With(x => x.isTrafficParticipant = true);
        }
    }
}
