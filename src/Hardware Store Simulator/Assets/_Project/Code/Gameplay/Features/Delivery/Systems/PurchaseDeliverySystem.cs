using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;
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
                    _events.EmitNotification(LocalizedTexts.Text(
                        LocalizationKey.NotificationAcceptCurrentDeliveryFirst));
                    continue;
                }

                GameEntity store =
                    _gameContext.GetEntityWithEntityId(terminal.StoreEntityId);
                GameEntity customerVisit =
                    _gameContext.GetEntityWithCustomerVisitStoreEntityId(store.EntityId);
                if (customerVisit == null)
                {
                    _events.EmitNotification(LocalizedTexts.Text(
                        LocalizationKey.NotificationWaitForCustomer));
                    continue;
                }

                if (customerVisit.isCustomerVisitCompleted ||
                    customerVisit.isCustomerVisitReturning ||
                    customerVisit.isCustomerVisitDeparting)
                {
                    _events.EmitNotification(LocalizedTexts.Text(
                        LocalizationKey.NotificationOrderCompletedWaitCustomer));
                    continue;
                }

                if (customerVisit.isCustomerVisitArriving ||
                    customerVisit.isCustomerVisitConsulting)
                {
                    _events.EmitNotification(
                        customerVisit.isCustomerVisitArriving
                            ? LocalizedTexts.Text(
                                LocalizationKey.NotificationWaitForCustomerConsultation)
                            : LocalizedTexts.Text(
                                LocalizationKey.NotificationConsultAtCounterFirst));
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
                var requiredLines = new List<GameEntity>(2);
                foreach (GameEntity line in
                         _gameContext.GetEntitiesWithOrderEntityId(customerVisit.EntityId))
                {
                    ValidateOrderLine(customerVisit, line);
                    requiredLines.Add(line);
                    if (line.ProductType != terminal.SelectedProductType)
                        continue;
                    if (selectedLine != null)
                        throw new InvalidOperationException(
                            $"Customer visit {customerVisit.EntityId} has duplicate order " +
                            $"lines for {line.ProductType}.");

                    selectedLine = line;
                }

                if (requiredLines.Count == 0)
                    throw new InvalidOperationException(
                        $"Customer visit {customerVisit.EntityId} has no order lines.");
                if (selectedLine == null)
                {
                    requiredLines.Sort((left, right) =>
                        left.LineIndex.CompareTo(right.LineIndex));
                    LocalizedText notification = requiredLines.Count switch
                    {
                        1 => LocalizedTexts.Text(
                            LocalizationKey.NotificationCurrentOrderNeedsOneProduct,
                            LocalizedTexts.ProductName(requiredLines[0].ProductType)),
                        2 => LocalizedTexts.Text(
                            LocalizationKey.NotificationCurrentOrderNeedsTwoProducts,
                            LocalizedTexts.ProductName(requiredLines[0].ProductType),
                            LocalizedTexts.ProductName(requiredLines[1].ProductType)),
                        _ => throw new InvalidOperationException(
                            $"Customer visit {customerVisit.EntityId} has more than two " +
                            "order lines.")
                    };
                    _events.EmitNotification(notification);
                    continue;
                }

                int remainingCount =
                    selectedLine.RequiredProductCount - selectedLine.LoadedProductCount;
                if (selectedLine.AvailableProductCount >= remainingCount)
                {
                    _events.EmitNotification(LocalizedTexts.Text(
                        LocalizationKey.NotificationProductStockSufficient,
                        LocalizedTexts.ProductName(selectedLine.ProductType),
                        selectedLine.AvailableProductCount,
                        remainingCount));
                    continue;
                }

                GameEntity storageZone =
                    _gameContext.GetEntityWithEntityId(terminal.StorageZoneEntityId);
                DeliveryConfig deliveryConfig =
                    _staticData.GetDelivery(terminal.SelectedProductType);
                int freeSlotCount = storageZone.Slots.Length - storageZone.OccupiedStorageSlotCount;
                if (freeSlotCount < deliveryConfig.ProductCount)
                {
                    _events.EmitNotification(LocalizedTexts.Text(
                        LocalizationKey.NotificationStorageSpaceInsufficient,
                        freeSlotCount,
                        deliveryConfig.ProductCount));
                    continue;
                }

                if (store.Money < deliveryConfig.TotalCost)
                {
                    _events.EmitNotification(LocalizedTexts.Text(
                        LocalizationKey.NotificationMoneyInsufficient,
                        deliveryConfig.TotalCost));
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
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationDeliveryOrdered,
                    LocalizedTexts.ProductName(delivery.ProductType),
                    delivery.DeliveryProductCount,
                    LocalizedTexts.ProductUnit(delivery.ProductType),
                    delivery.DeliveryCost));
                _events.EmitAudio(AudioCueId.DeliveryPurchased);
                request.isPurchaseDeliverySucceeded = true;
            }
        }

        private static void ValidateOrderLine(GameEntity visit, GameEntity line)
        {
            if (!line.isOrderLine || line.isDestructed || !line.hasEntityId ||
                !line.hasOrderEntityId || !line.hasStorageZoneEntityId ||
                !line.hasLineIndex || !line.hasProductType ||
                !line.hasRequiredProductCount ||
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
