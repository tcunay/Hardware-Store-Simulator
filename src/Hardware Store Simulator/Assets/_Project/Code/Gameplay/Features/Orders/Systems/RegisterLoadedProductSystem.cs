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
        private static readonly Comparison<GameEntity> PendingProductComparison =
            ComparePendingProducts;

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
                    GameMatcher.ProductType,
                    GameMatcher.LoadingSlotIndex)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            _loadedProducts.GetEntities(_buffer);
            _buffer.Sort(PendingProductComparison);
            int groupStart = 0;
            while (groupStart < _buffer.Count)
            {
                int orderLineEntityId = _buffer[groupStart].OrderLineEntityId;
                int groupEnd = groupStart + 1;
                while (groupEnd < _buffer.Count &&
                       _buffer[groupEnd].OrderLineEntityId == orderLineEntityId)
                {
                    groupEnd++;
                }

                RegisterGroup(groupStart, groupEnd);
                groupStart = groupEnd;
            }
        }

        private void RegisterGroup(int groupStart, int groupEnd)
        {
            GameEntity firstProduct = _buffer[groupStart];
            GameEntity orderLine = _gameContext.GetEntityWithEntityId(
                firstProduct.OrderLineEntityId);
            ValidateOrderLine(firstProduct, orderLine);
            GameEntity visit = _gameContext.GetEntityWithEntityId(
                orderLine.OrderEntityId);
            ValidateVisit(firstProduct, visit, orderLine);

            int pendingCount = groupEnd - groupStart;
            int linkedProductCount = CountLinkedProducts(orderLine);
            int loaded = orderLine.LoadedProductCount;
            int required = orderLine.RequiredProductCount;
            if (linkedProductCount != loaded + pendingCount)
            {
                throw new InvalidOperationException(
                    $"Order line {orderLine.EntityId} contains {linkedProductCount} linked " +
                    $"products before batch registration, expected {loaded + pendingCount}.");
            }
            if (loaded + pendingCount > required)
            {
                throw new InvalidOperationException(
                    $"Order line {orderLine.EntityId} cannot register {pendingCount} " +
                    "additional products.");
            }

            for (int index = groupStart; index < groupEnd; index++)
            {
                GameEntity product = _buffer[index];
                ValidateOrderLine(product, orderLine);
                ValidateVisit(product, visit, orderLine);
                loaded++;
                orderLine.ReplaceLoadedProductCount(loaded);
                product.isProductLoaded = false;
                _events.EmitNotification(LocalizedTexts.Text(
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
                     _gameContext.GetEntitiesWithOrderLineEntityId(
                         orderLine.EntityId))
            {
                if (!product.isProduct || !product.isLoaded ||
                    product.isDestructed || !product.hasEntityId ||
                    !product.hasProductType ||
                    product.OrderLineEntityId != orderLine.EntityId ||
                    product.ProductType != orderLine.ProductType ||
                    !product.hasLoadingSlotIndex)
                {
                    throw new InvalidOperationException(
                        $"Order line {orderLine.EntityId} has an invalid linked product.");
                }
                productCount++;
            }
            return productCount;
        }

        private static void ValidateOrderLine(GameEntity product,
            GameEntity orderLine)
        {
            if (orderLine == null || !orderLine.isOrderLine ||
                orderLine.isDestructed || !orderLine.hasEntityId ||
                !orderLine.hasOrderEntityId || !orderLine.hasProductType ||
                !orderLine.hasRequiredProductCount ||
                !orderLine.hasLoadedProductCount ||
                orderLine.EntityId != product.OrderLineEntityId ||
                orderLine.ProductType != product.ProductType)
            {
                throw new InvalidOperationException(
                    $"Product {product.EntityId} cannot be registered for order line " +
                    $"{product.OrderLineEntityId}.");
            }
        }

        private static void ValidateVisit(GameEntity product, GameEntity visit,
            GameEntity orderLine)
        {
            if (visit == null || !visit.isCustomerVisitLoading ||
                !visit.isOrder || !visit.hasEntityId ||
                visit.EntityId != orderLine.OrderEntityId)
            {
                throw new InvalidOperationException(
                    $"Product {product.EntityId} references inactive customer visit " +
                    $"{orderLine.OrderEntityId}.");
            }
        }

        private static int ComparePendingProducts(GameEntity left,
            GameEntity right)
        {
            int lineComparison = left.OrderLineEntityId.CompareTo(
                right.OrderLineEntityId);
            return lineComparison != 0
                ? lineComparison
                : left.EntityId.CompareTo(right.EntityId);
        }
    }
}
