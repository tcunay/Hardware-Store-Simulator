using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Scene;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class StoreFactory : IStoreFactory
    {
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;
        private readonly IInteractionTargetFactory _interactionTargetFactory;

        public StoreFactory(IIdentifierService identifiers, IStaticDataService staticData,
            IInteractionTargetFactory interactionTargetFactory)
        {
            _identifiers = identifiers;
            _staticData = staticData;
            _interactionTargetFactory = interactionTargetFactory;
        }

        public GameEntity Create(IStoreSceneData sceneData)
        {
            Pose deliveryPose = sceneData.GetSpawnPoint(SpawnPointId.DeliveryVehicle);
            GameEntity storageZone = _interactionTargetFactory.CreateStorageZone();
            GameEntity store = CreateEntity.Empty(_identifiers.Next())
                .AddMoney(_staticData.Economy.InitialMoney)
                .AddCustomerCooldownRemaining(_staticData.CustomerVehicle.FirstCustomerDelay)
                .With(x => x.isStore = true);

            GameEntity orderCounter = _interactionTargetFactory.CreateOrderCounter(store.EntityId);

            GameEntity procurementTerminal =
                _interactionTargetFactory.CreateProcurementTerminal(
                    store.EntityId,
                    storageZone.EntityId,
                    deliveryPose);

            store.AddOrderCounterEntityId(orderCounter.EntityId);
            store.AddProcurementTerminalEntityId(procurementTerminal.EntityId);
            store.AddStorageZoneEntityId(storageZone.EntityId);
            return store;
        }
    }
}
