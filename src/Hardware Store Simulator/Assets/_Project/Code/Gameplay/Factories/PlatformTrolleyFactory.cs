using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class PlatformTrolleyFactory : IPlatformTrolleyFactory
    {
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;

        public PlatformTrolleyFactory(IIdentifierService identifiers,
            IStaticDataService staticData)
        {
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public GameEntity Create(Pose at, int storeEntityId)
        {
            PlatformTrolleyConfig config = _staticData.PlatformTrolley;
            return CreateEntity.Empty(_identifiers.Next())
                .AddViewPrefab(config.ViewPrefab)
                .AddSpawnPosition(at.position)
                .AddSpawnRotation(at.rotation)
                .AddTrolleyStoreEntityId(storeEntityId)
                .AddTrolleyCapacity(config.Capacity)
                .AddOccupiedTrolleySlotCount(0)
                .AddTrolleyMovementSpeed(config.MovementSpeed)
                .AddTrolleyFollowDistance(config.FollowDistance)
                .With(x => x.isPlatformTrolley = true)
                .With(x => x.isInteractable = true);
        }
    }
}
