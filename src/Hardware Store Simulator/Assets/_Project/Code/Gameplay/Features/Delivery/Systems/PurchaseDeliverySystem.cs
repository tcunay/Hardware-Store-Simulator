using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Delivery.Systems
{
    public sealed class PurchaseDeliverySystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IStaticDataService _staticData;
        private readonly IDeliveryFactory _deliveryFactory;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public PurchaseDeliverySystem(GameContext gameContext, IStaticDataService staticData,
            IDeliveryFactory deliveryFactory, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _staticData = staticData;
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
                if (!terminal.hasSelectedProductType)
                    throw new System.InvalidOperationException(
                        $"Procurement terminal {terminal.EntityId} has no selected product type.");

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
                GameEntity customerVisit =
                    _gameContext.GetEntityWithCustomerVisitStoreEntityId(store.EntityId);
                if (customerVisit == null)
                {
                    _events.EmitNotification(
                        "Дождитесь клиента, чтобы выбрать поставку под его заказ");
                    continue;
                }

                if (customerVisit.isCustomerVisitCompleted ||
                    customerVisit.isCustomerVisitDeparting)
                {
                    _events.EmitNotification(
                        "Текущий заказ уже выполнен — дождитесь следующего клиента");
                    continue;
                }

                if (terminal.SelectedProductType != customerVisit.RequiredProductType)
                {
                    ProductConfig requiredProduct =
                        _staticData.GetProduct(customerVisit.RequiredProductType);
                    _events.EmitNotification(
                        $"Для текущего заказа нужен товар: {requiredProduct.DisplayName}");
                    continue;
                }

                GameEntity storageZone =
                    _gameContext.GetEntityWithEntityId(terminal.StorageZoneEntityId);
                DeliveryConfig deliveryConfig =
                    _staticData.GetDelivery(terminal.SelectedProductType);
                int freeSlotCount = storageZone.Slots.Length - storageZone.OccupiedStorageSlotCount;
                if (freeSlotCount < deliveryConfig.ProductCount)
                {
                    _events.EmitNotification(
                        $"Недостаточно места на складе: свободно {freeSlotCount}/" +
                        $"{deliveryConfig.ProductCount}");
                    continue;
                }

                if (store.Money < deliveryConfig.TotalCost)
                {
                    _events.EmitNotification(
                        $"Недостаточно денег на поставку: нужно " +
                        $"{deliveryConfig.TotalCost:N0} ₽");
                    continue;
                }

                var deliveryPose = new Pose(
                    terminal.DeliverySpawnPosition,
                    terminal.DeliverySpawnRotation);
                GameEntity delivery = _deliveryFactory.Create(
                    terminal.SelectedProductType,
                    terminal.EntityId,
                    store.EntityId,
                    deliveryPose);

                store.ReplaceMoney(store.Money - delivery.DeliveryCost);
                ProductConfig productConfig = _staticData.GetProduct(delivery.ProductType);
                _events.EmitNotification(
                    $"Поставка заказана • товар: {productConfig.DisplayName} • " +
                    $"количество: {delivery.DeliveryProductCount} {productConfig.UnitLabel} • " +
                    $"−{delivery.DeliveryCost:N0} ₽");
                _events.EmitAudio(AudioCueId.DeliveryPurchased);
            }
        }
    }
}
