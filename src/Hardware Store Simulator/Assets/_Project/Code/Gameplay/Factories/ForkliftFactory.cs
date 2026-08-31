using System;
using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class ForkliftFactory : IForkliftFactory
    {
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;

        public ForkliftFactory(IIdentifierService identifiers,
            IStaticDataService staticData)
        {
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public GameEntity Create(Pose at, int storeEntityId)
        {
            if (storeEntityId <= 0)
                throw new ArgumentOutOfRangeException(nameof(storeEntityId));

            ForkliftConfig config = _staticData.Forklift;
            return CreateEntity.Empty(_identifiers.Next())
                .AddViewPrefab(config.ViewPrefab)
                .AddSpawnPosition(at.position)
                .AddSpawnRotation(at.rotation)
                .AddForkliftStoreEntityId(storeEntityId)
                .AddForkliftForkHeight(config.InitialForkHeight)
                .AddForkliftMinForkHeight(config.MinForkHeight)
                .AddForkliftMaxForkHeight(config.MaxForkHeight)
                .AddForkliftLiftSpeed(config.LiftSpeed)
                .AddForkliftForwardSpeed(config.ForwardSpeed)
                .AddForkliftReverseSpeed(config.ReverseSpeed)
                .AddForkliftSteeringSpeed(config.SteeringSpeed)
                .AddTrafficControlPolicy(TrafficControlPolicyId.Uncontrolled)
                .AddTrafficPriority((int)TrafficPriorityId.PlayerVehicle)
                .AddTrafficDesiredVelocity(Vector3.zero)
                .AddTrafficIntentDistance(0f)
                .AddTrafficAngularIntent(0f)
                .AddTrafficPreviousPosition(at.position)
                .With(x => x.isForklift = true)
                .With(x => x.isTrafficParticipant = true)
                .With(x => x.isInteractable = true);
        }
    }
}
