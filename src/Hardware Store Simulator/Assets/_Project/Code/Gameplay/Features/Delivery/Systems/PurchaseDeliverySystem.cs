using Entitas;
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
                GameEntity terminal =
                    _gameContext.GetEntityWithEntityId(request.TargetEntityId);
                if (!terminal.isProcurementTerminal)
                    continue;

                GameEntity player =
                    _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                if (player.StoreEntityId != terminal.StoreEntityId)
                    continue;

                if (_gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                        terminal.EntityId) != null)
                {
                    _events.EmitNotification("Сначала примите текущую поставку на склад");
                    continue;
                }

                GameEntity store =
                    _gameContext.GetEntityWithEntityId(terminal.StoreEntityId);
                GameEntity storageZone =
                    _gameContext.GetEntityWithEntityId(terminal.StorageZoneEntityId);
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

                var deliveryPose = new Pose(
                    terminal.DeliverySpawnPosition,
                    terminal.DeliverySpawnRotation);
                GameEntity delivery = _deliveryFactory.Create(
                    terminal.EntityId,
                    store.EntityId,
                    deliveryPose);

                store.ReplaceMoney(store.Money - delivery.DeliveryCost);
                _events.EmitNotification(
                    $"Поставка заказана: {delivery.DeliveryProductCount} мешка цемента, " +
                    $"−{delivery.DeliveryCost:N0} ₽");
                _events.EmitAudio(AudioCueId.DeliveryPurchased);
            }
        }
    }
}
