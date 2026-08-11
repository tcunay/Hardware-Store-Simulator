using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Features.Orders.Systems
{
    public sealed class AcceptOrderSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;
        private readonly List<GameEntity> _missingLines = new(2);

        public AcceptOrderSystem(GameContext gameContext, IGameEventFactory events)
        {
            _gameContext = gameContext;
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
                GameEntity orderCounter =
                    _gameContext.GetEntityWithEntityId(request.TargetEntityId);
                if (!orderCounter.isOrderCounter)
                    continue;

                GameEntity player =
                    _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                if (player.StoreEntityId != orderCounter.StoreEntityId)
                    continue;

                GameEntity customerVisit =
                    _gameContext.GetEntityWithCustomerVisitStoreEntityId(
                        orderCounter.StoreEntityId);
                if (customerVisit == null || !customerVisit.isCustomerVisitWaiting)
                    continue;
                if (!customerVisit.isOrder || !customerVisit.hasEntityId)
                    throw new InvalidOperationException(
                        $"Waiting customer visit {customerVisit.EntityId} has no order.");

                _missingLines.Clear();
                int lineCount = 0;
                int totalRequiredCount = 0;
                foreach (GameEntity line in
                         _gameContext.GetEntitiesWithOrderEntityId(customerVisit.EntityId))
                {
                    ValidateOrderLine(customerVisit, line);
                    lineCount++;
                    totalRequiredCount = checked(
                        totalRequiredCount + line.RequiredProductCount);
                    if (line.AvailableProductCount >= line.RequiredProductCount)
                        continue;

                    _missingLines.Add(line);
                }

                if (lineCount == 0)
                    throw new InvalidOperationException(
                        $"Customer visit {customerVisit.EntityId} has no order lines.");
                if (_missingLines.Count > 0)
                {
                    _missingLines.Sort((left, right) =>
                        left.LineIndex.CompareTo(right.LineIndex));
                    LocalizedText notification = _missingLines.Count switch
                    {
                        1 => LocalizedTexts.Text(
                            LocalizationKey.NotificationOrderStockMissingOne,
                            LocalizedTexts.ProductName(_missingLines[0].ProductType),
                            _missingLines[0].AvailableProductCount,
                            _missingLines[0].RequiredProductCount),
                        2 => LocalizedTexts.Text(
                            LocalizationKey.NotificationOrderStockMissingTwo,
                            LocalizedTexts.ProductName(_missingLines[0].ProductType),
                            _missingLines[0].AvailableProductCount,
                            _missingLines[0].RequiredProductCount,
                            LocalizedTexts.ProductName(_missingLines[1].ProductType),
                            _missingLines[1].AvailableProductCount,
                            _missingLines[1].RequiredProductCount),
                        _ => throw new InvalidOperationException(
                            $"Customer visit {customerVisit.EntityId} has more than two " +
                            "missing order lines.")
                    };
                    _events.EmitNotification(notification);
                    continue;
                }

                customerVisit.isCustomerVisitWaiting = false;
                customerVisit.isCustomerVisitLoading = true;
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationOrderAccepted,
                    lineCount,
                    totalRequiredCount));
                _events.EmitAudio(AudioCueId.OrderAccepted);
            }
        }

        private static void ValidateOrderLine(GameEntity visit, GameEntity line)
        {
            if (!line.isOrderLine || line.isDestructed || !line.hasEntityId ||
                !line.hasOrderEntityId || !line.hasStorageZoneEntityId ||
                !line.hasLineIndex || !line.hasProductType ||
                !line.hasRequiredProductCount || !line.hasAvailableProductCount ||
                !line.hasLoadedProductCount)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid order line.");
            }
            if (line.OrderEntityId != visit.EntityId ||
                line.StorageZoneEntityId != visit.StorageZoneEntityId ||
                line.RequiredProductCount <= 0 || line.AvailableProductCount < 0 ||
                line.LoadedProductCount != 0)
            {
                throw new InvalidOperationException(
                    $"Order line {line.EntityId} cannot be accepted.");
            }
        }
    }
}
