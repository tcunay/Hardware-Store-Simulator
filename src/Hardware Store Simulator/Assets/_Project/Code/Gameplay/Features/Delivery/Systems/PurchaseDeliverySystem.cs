using System;
using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Delivery.Systems
{
    public sealed class PurchaseDeliverySystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IDeliveryFactory _deliveryFactory;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public PurchaseDeliverySystem(GameContext gameContext, IDeliveryFactory deliveryFactory,
            IGameEventFactory events)
        {
            _gameContext = gameContext;
            _deliveryFactory = deliveryFactory;
            _events = events;
            _requests = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.InteractionRequest,
                GameMatcher.SourceEntityId,
                GameMatcher.TargetEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity request in _requests)
            {
                GameEntity terminal = _gameContext.GetRequiredEntity(
                    request.TargetEntityId,
                    "interaction target");
                if (!terminal.isProcurementTerminal)
                    continue;

                GameEntity player = _gameContext.GetRequiredEntity(
                    request.SourceEntityId,
                    "interaction source");
                if (!player.isPlayer)
                    throw new InvalidOperationException(
                        $"Interaction source {request.SourceEntityId} is not a player.");
                if (!player.hasStoreEntityId)
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} has no store relation.");
                if (!terminal.hasStoreEntityId || !terminal.hasStorageZoneEntityId)
                    throw new InvalidOperationException(
                        $"Procurement terminal {terminal.EntityId} has incomplete store relations.");
                if (player.StoreEntityId != terminal.StoreEntityId)
                    continue;

                GameEntity store = _gameContext.GetRequiredEntity(terminal.StoreEntityId, "terminal store");
                if (!store.isStore || !store.hasMoney ||
                    !store.hasProcurementTerminalEntityId || !store.hasStorageZoneEntityId)
                    throw new InvalidOperationException(
                        $"Entity {terminal.StoreEntityId} is not a configured store.");
                if (store.ProcurementTerminalEntityId != terminal.EntityId ||
                    store.StorageZoneEntityId != terminal.StorageZoneEntityId)
                    throw new InvalidOperationException(
                        $"Procurement terminal {terminal.EntityId} does not match store {store.EntityId}.");

                if (terminal.hasDeliveryEntityId)
                {
                    GameEntity activeDelivery =
                        _gameContext.GetRequiredEntity(terminal.DeliveryEntityId, "active delivery");
                    if (!activeDelivery.isDelivery || !activeDelivery.isDeliveryActive)
                        throw new InvalidOperationException(
                            $"Procurement terminal {terminal.EntityId} has a stale delivery relation.");

                    _events.EmitNotification("Сначала примите текущую поставку на склад");
                    continue;
                }

                GameEntity storageZone =
                    _gameContext.GetRequiredEntity(terminal.StorageZoneEntityId, "terminal storage zone");
                if (!storageZone.isStorageZone)
                    throw new InvalidOperationException(
                        $"Entity {terminal.StorageZoneEntityId} is not a storage zone.");
                if (!storageZone.hasSlots)
                    throw new InvalidOperationException(
                        $"Storage zone {storageZone.EntityId} has no registered slots.");
                if (terminal.DeliveryProductCount <= 0 ||
                    terminal.DeliveryProductCount > storageZone.Slots.Length)
                    throw new InvalidOperationException(
                        $"Delivery size {terminal.DeliveryProductCount} does not fit storage " +
                        $"capacity {storageZone.Slots.Length}.");

                if (!storageZone.hasOccupiedStorageSlotCount)
                    throw new InvalidOperationException(
                        $"Storage zone {storageZone.EntityId} has no occupancy snapshot.");

                int freeSlotCount = storageZone.Slots.Length - storageZone.OccupiedStorageSlotCount;
                if (freeSlotCount < terminal.DeliveryProductCount)
                {
                    _events.EmitNotification(
                        $"Недостаточно места на складе: свободно {freeSlotCount}/" +
                        $"{terminal.DeliveryProductCount}");
                    continue;
                }

                if (store.Money < terminal.DeliveryCost)
                {
                    _events.EmitNotification(
                        $"Недостаточно денег на поставку: нужно {terminal.DeliveryCost:N0} ₽");
                    continue;
                }

                var deliveryPose = new Pose(terminal.SpawnPosition, terminal.SpawnRotation);
                GameEntity delivery = _deliveryFactory.Create(
                    terminal.EntityId,
                    store.EntityId,
                    deliveryPose);

                if (delivery.DeliveryCost != terminal.DeliveryCost ||
                    delivery.ProductType != terminal.ProductType ||
                    delivery.DeliveryProductCount != terminal.DeliveryProductCount)
                    throw new InvalidOperationException(
                        "Procurement terminal and delivery config snapshots do not match.");

                terminal.AddDeliveryEntityId(delivery.EntityId);
                store.ReplaceMoney(store.Money - delivery.DeliveryCost);
                _events.EmitNotification(
                    $"Поставка заказана: {delivery.DeliveryProductCount} мешка цемента, " +
                    $"−{delivery.DeliveryCost:N0} ₽");
                _events.EmitAudio(AudioCueId.DeliveryPurchased);
            }
        }

    }
}
