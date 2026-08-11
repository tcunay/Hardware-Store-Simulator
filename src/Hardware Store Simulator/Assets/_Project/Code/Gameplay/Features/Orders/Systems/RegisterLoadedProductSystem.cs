using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Features.Orders.Systems
{
    public sealed class RegisterLoadedProductSystem : IExecuteSystem
    {
        private readonly IGameEventFactory _events;
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _loadedProducts;
        private readonly List<GameEntity> _buffer = new(8);

        public RegisterLoadedProductSystem(GameContext gameContext,
            IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _loadedProducts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.EntityId,
                    GameMatcher.ProductLoaded,
                    GameMatcher.Loaded,
                    GameMatcher.OrderLineEntityId,
                    GameMatcher.ProductType)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity product in _loadedProducts.GetEntities(_buffer))
            {
                GameEntity orderLine = _gameContext.GetEntityWithEntityId(
                    product.OrderLineEntityId);
                ValidateOrderLine(product, orderLine);
                GameEntity visit = _gameContext.GetEntityWithEntityId(
                    orderLine.OrderEntityId);
                ValidateRelations(product, visit, orderLine);

                int loaded = orderLine.LoadedProductCount;
                int required = orderLine.RequiredProductCount;
                if (loaded >= required)
                    throw new InvalidOperationException(
                        $"Order line {orderLine.EntityId} already contains all required products.");

                int linkedProductCount = CountLinkedProducts(orderLine);
                if (linkedProductCount != loaded + 1)
                {
                    throw new InvalidOperationException(
                        $"Order line {orderLine.EntityId} contains {linkedProductCount} linked " +
                        $"products before registration, expected {loaded + 1}.");
                }

                loaded++;
                orderLine.ReplaceLoadedProductCount(loaded);
                product.isProductLoaded = false;
                _events.EmitNotification(
                    LocalizedTexts.Text(
                        LocalizationKey.NotificationProductLoaded,
                        LocalizedTexts.ProductName(product.ProductType),
                        loaded,
                        required,
                        LocalizedTexts.ProductUnit(product.ProductType)));
                _events.EmitAudio(AudioCueId.Load);
            }
        }

        private int CountLinkedProducts(GameEntity orderLine)
        {
            int productCount = 0;
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithOrderLineEntityId(orderLine.EntityId))
            {
                if (!product.isProduct || !product.isLoaded || product.isDestructed ||
                    !product.hasProductType ||
                    product.OrderLineEntityId != orderLine.EntityId ||
                    product.ProductType != orderLine.ProductType)
                {
                    throw new InvalidOperationException(
                        $"Order line {orderLine.EntityId} has an invalid linked product.");
                }

                productCount++;
            }

            return productCount;
        }

        private static void ValidateOrderLine(GameEntity product, GameEntity orderLine)
        {
            if (!orderLine.isOrderLine || orderLine.isDestructed ||
                !orderLine.hasEntityId || !orderLine.hasOrderEntityId ||
                !orderLine.hasProductType || !orderLine.hasRequiredProductCount ||
                !orderLine.hasLoadedProductCount ||
                orderLine.EntityId != product.OrderLineEntityId ||
                orderLine.ProductType != product.ProductType)
            {
                throw new InvalidOperationException(
                    $"Product {product.EntityId} cannot be registered for order line " +
                    $"{product.OrderLineEntityId}.");
            }
        }

        private static void ValidateRelations(GameEntity product, GameEntity visit,
            GameEntity orderLine)
        {
            if (!visit.isCustomerVisitLoading || !visit.isOrder || !visit.hasEntityId ||
                visit.EntityId != orderLine.OrderEntityId)
            {
                throw new InvalidOperationException(
                    $"Product {product.EntityId} references inactive customer visit " +
                    $"{visit.EntityId}.");
            }
        }
    }
}
