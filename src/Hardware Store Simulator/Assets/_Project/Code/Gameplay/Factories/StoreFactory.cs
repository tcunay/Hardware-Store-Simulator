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
        private readonly ICustomerFlowFactory _customerFlowFactory;

        public StoreFactory(IIdentifierService identifiers, IStaticDataService staticData,
            IInteractionTargetFactory interactionTargetFactory,
            ICustomerFlowFactory customerFlowFactory)
        {
            _identifiers = identifiers;
            _staticData = staticData;
            _interactionTargetFactory = interactionTargetFactory;
            _customerFlowFactory = customerFlowFactory;
        }

        public GameEntity Create(IStoreSceneData sceneData)
        {
            Pose deliveryPose = sceneData.GetSpawnPoint(SpawnPointId.DeliveryVehicle);
            Pose trolleySpawnPose = sceneData.GetSpawnPoint(SpawnPointId.PlatformTrolley);
            int initialMoney = _staticData.Economy.InitialMoney;
            GameEntity storageZone = _interactionTargetFactory.CreateStorageZone();
            GameEntity store = CreateEntity.Empty(_identifiers.Next())
                .AddMoney(initialMoney)
                .AddNextProjectSequenceIndex(0)
                .AddNextCustomerArrivalSequence(0)
                .AddCompletedOrderCount(0)
                .AddDayNumber(1)
                .AddCurrentDayMinute(_staticData.StoreDay.StartMinute)
                .AddDayOpeningBalance(initialMoney)
                .AddDayRevenue(0)
                .AddDayProcurementExpenses(0)
                .AddDayUpgradeExpenses(0)
                .AddDayPayrollExpenses(0)
                .AddDayCompletedOrderCount(0)
                .AddDayLostCustomerCount(0)
                .With(x => x.isStorePreparing = true)
                .With(x => x.isStore = true);

            GameEntity orderCounter = _interactionTargetFactory.CreateOrderCounter(store.EntityId);

            GameEntity procurementTerminal =
                _interactionTargetFactory.CreateProcurementTerminal(
                    store.EntityId,
                    storageZone.EntityId,
                    deliveryPose);

            GameEntity trolleyUpgradeTerminal =
                _interactionTargetFactory.CreateTrolleyUpgradeTerminal(
                    store.EntityId,
                    trolleySpawnPose);

            GameEntity storeControlTerminal =
                _interactionTargetFactory.CreateStoreControlTerminal(store.EntityId);

            store.AddOrderCounterEntityId(orderCounter.EntityId);
            store.AddProcurementTerminalEntityId(procurementTerminal.EntityId);
            store.AddStorageZoneEntityId(storageZone.EntityId);
            store.AddTrolleyUpgradeTerminalEntityId(trolleyUpgradeTerminal.EntityId);
            store.AddStoreControlTerminalEntityId(storeControlTerminal.EntityId);
            _customerFlowFactory.Create(store, sceneData.GetCustomerFlowLayout());
            return store;
        }
    }
}
