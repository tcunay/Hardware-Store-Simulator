using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Orders.Systems
{
    public sealed class AcceptOrderSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IStaticDataService _staticData;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;
        private readonly List<string> _missingProducts = new(4);

        public AcceptOrderSystem(GameContext gameContext, IStaticDataService staticData,
            IGameEventFactory events)
        {
            _gameContext = gameContext;
            _staticData = staticData;
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

                _missingProducts.Clear();
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

                    var product = _staticData.GetProduct(line.ProductType);
                    _missingProducts.Add(
                        $"{product.DisplayName} " +
                        $"{line.AvailableProductCount}/{line.RequiredProductCount}");
                }

                if (lineCount == 0)
                    throw new InvalidOperationException(
                        $"Customer visit {customerVisit.EntityId} has no order lines.");
                if (_missingProducts.Count > 0)
                {
                    _events.EmitNotification(
                        $"Недостаточно товара на складе: " +
                        $"{string.Join(" • ", _missingProducts)}");
                    continue;
                }

                customerVisit.isCustomerVisitWaiting = false;
                customerVisit.isCustomerVisitLoading = true;
                _events.EmitNotification(
                    $"Заказ принят • позиций: {lineCount} • товаров: {totalRequiredCount}");
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
