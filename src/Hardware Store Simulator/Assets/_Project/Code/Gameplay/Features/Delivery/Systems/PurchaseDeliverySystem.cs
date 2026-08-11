using System;
using System.Collections.Generic;
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
                GameMatcher.PurchaseDeliveryRequest,
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
                    throw new InvalidOperationException(
                        $"Purchase request targets non-procurement entity " +
                        $"{terminal.EntityId}.");
                if (!terminal.hasSelectedProductType)
                    throw new InvalidOperationException(
                        $"Procurement terminal {terminal.EntityId} has no selected product type.");

                GameEntity player =
                    _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                if (!player.isPlayer || !player.isModalOpen ||
                    !player.hasProcurementTerminalEntityId ||
                    player.ProcurementTerminalEntityId != terminal.EntityId ||
                    player.StoreEntityId != terminal.StoreEntityId)
                {
                    throw new InvalidOperationException(
                        $"Purchase request source {player.EntityId} does not own procurement " +
                        $"modal for terminal {terminal.EntityId}.");
                }

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
                    customerVisit.isCustomerVisitReturning ||
                    customerVisit.isCustomerVisitDeparting)
                {
                    _events.EmitNotification(
                        "Текущий заказ уже выполнен — дождитесь следующего клиента");
                    continue;
                }

                if (customerVisit.isCustomerVisitArriving ||
                    customerVisit.isCustomerVisitConsulting)
                {
                    _events.EmitNotification(
                        customerVisit.isCustomerVisitArriving
                            ? "Дождитесь клиента, чтобы согласовать предложение"
                            : "Сначала согласуйте предложение с клиентом у стойки");
                    continue;
                }

                if (!customerVisit.isOrder || !customerVisit.hasEntityId ||
                    (!customerVisit.isCustomerVisitWaiting &&
                     !customerVisit.isCustomerVisitLoading))
                {
                    throw new InvalidOperationException(
                        $"Customer visit {customerVisit.EntityId} cannot procure order stock.");
                }

                GameEntity selectedLine = null;
                var requiredProductNames = new List<string>(4);
                foreach (GameEntity line in
                         _gameContext.GetEntitiesWithOrderEntityId(customerVisit.EntityId))
                {
                    ValidateOrderLine(customerVisit, line);
                    requiredProductNames.Add(
                        _staticData.GetProduct(line.ProductType).DisplayName);
                    if (line.ProductType != terminal.SelectedProductType)
                        continue;
                    if (selectedLine != null)
                        throw new InvalidOperationException(
                            $"Customer visit {customerVisit.EntityId} has duplicate order " +
                            $"lines for {line.ProductType}.");

                    selectedLine = line;
                }

                if (requiredProductNames.Count == 0)
                    throw new InvalidOperationException(
                        $"Customer visit {customerVisit.EntityId} has no order lines.");
                if (selectedLine == null)
                {
                    _events.EmitNotification(
                        $"Для текущего заказа нужны: " +
                        $"{string.Join(", ", requiredProductNames)}");
                    continue;
                }

                int remainingCount =
                    selectedLine.RequiredProductCount - selectedLine.LoadedProductCount;
                if (selectedLine.AvailableProductCount >= remainingCount)
                {
                    ProductConfig selectedProduct =
                        _staticData.GetProduct(selectedLine.ProductType);
                    _events.EmitNotification(
                        $"Товара уже достаточно: {selectedProduct.DisplayName} • " +
                        $"доступно {selectedLine.AvailableProductCount}/{remainingCount}");
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
                request.isPurchaseDeliverySucceeded = true;
            }
        }

        private static void ValidateOrderLine(GameEntity visit, GameEntity line)
        {
            if (!line.isOrderLine || line.isDestructed || !line.hasEntityId ||
                !line.hasOrderEntityId || !line.hasStorageZoneEntityId ||
                !line.hasProductType || !line.hasRequiredProductCount ||
                !line.hasAvailableProductCount || !line.hasLoadedProductCount)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid order line.");
            }
            if (line.OrderEntityId != visit.EntityId ||
                line.StorageZoneEntityId != visit.StorageZoneEntityId ||
                line.LoadedProductCount < 0 ||
                line.LoadedProductCount > line.RequiredProductCount)
            {
                throw new InvalidOperationException(
                    $"Order line {line.EntityId} has invalid runtime state.");
            }
        }
    }
}
