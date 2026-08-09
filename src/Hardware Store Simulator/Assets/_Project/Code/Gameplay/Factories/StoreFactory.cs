using System;
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
        private readonly IOrderFactory _orderFactory;
        private readonly IInteractionTargetFactory _interactionTargetFactory;

        public StoreFactory(IIdentifierService identifiers, IStaticDataService staticData,
            IOrderFactory orderFactory, IInteractionTargetFactory interactionTargetFactory)
        {
            _identifiers = identifiers;
            _staticData = staticData;
            _orderFactory = orderFactory;
            _interactionTargetFactory = interactionTargetFactory;
        }

        public GameEntity Create(IStoreSceneData sceneData)
        {
            Pose deliveryPose = sceneData.GetSpawnPoint(SpawnPointId.DeliveryVehicle);
            GameEntity storageZone = _interactionTargetFactory.CreateStorageZone(
                sceneData.GetSceneView(SceneViewId.StorageZone));
            GameEntity store = CreateEntity.Empty(_identifiers.Next())
                .AddMoney(_staticData.Economy.InitialMoney)
                .With(x => x.isStore = true);
            GameEntity order = _orderFactory.CreateOrder(store.EntityId, storageZone.EntityId);

            _interactionTargetFactory.CreateOrderCounter(
                sceneData.GetSceneView(SceneViewId.CustomerOrderCounter),
                order.EntityId);
            GameEntity customerLoadingZone =
                _interactionTargetFactory.CreateCustomerLoadingZone(
                    sceneData.GetSceneView(SceneViewId.CustomerLoadingZone),
                    order.EntityId);

            ValidateCapacity(customerLoadingZone, storageZone, order);

            GameEntity procurementTerminal =
                _interactionTargetFactory.CreateProcurementTerminal(
                    sceneData.GetSceneView(SceneViewId.ProcurementTerminal),
                    store.EntityId,
                    storageZone.EntityId,
                    deliveryPose);

            store.AddOrderEntityId(order.EntityId);
            store.AddProcurementTerminalEntityId(procurementTerminal.EntityId);
            store.AddStorageZoneEntityId(storageZone.EntityId);
            return store;
        }

        private void ValidateCapacity(GameEntity customerLoadingZone, GameEntity storageZone,
            GameEntity order)
        {
            if (!customerLoadingZone.hasSlots ||
                customerLoadingZone.Slots.Length < order.RequiredProductCount)
                throw new InvalidOperationException(
                    "The customer loading zone must contain enough slots to complete the order.");
            if (!storageZone.hasSlots ||
                storageZone.Slots.Length < _staticData.Delivery.ProductCount)
                throw new InvalidOperationException(
                    "The storage zone must contain enough slots for the complete delivery.");
        }
    }
}
