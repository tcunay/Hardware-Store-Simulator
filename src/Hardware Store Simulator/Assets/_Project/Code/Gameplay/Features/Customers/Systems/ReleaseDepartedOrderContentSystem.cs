using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class ReleaseDepartedOrderContentSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _visitBuffer = new(4);
        private readonly List<GameEntity> _lineBuffer = new(4);
        private readonly List<GameEntity> _productBuffer = new(8);

        public ReleaseDepartedOrderContentSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.CustomerVisitDeparting,
                    GameMatcher.Order,
                    GameMatcher.OrderRewarded,
                    GameMatcher.EntityId,
                    GameMatcher.RouteCompleted)
                .NoneOf(
                    GameMatcher.OrderContentReleased,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_visitBuffer))
                ReleaseContent(visit);
        }

        private void ReleaseContent(GameEntity visit)
        {
            int requiredProductCount = CollectOrderLines(visit);
            int loadedProductCount = CollectLoadedProducts(visit);
            if (loadedProductCount != requiredProductCount)
            {
                throw new InvalidOperationException(
                    $"Departed customer visit {visit.EntityId} contains " +
                    $"{loadedProductCount} products, but its order requires " +
                    $"{requiredProductCount}.");
            }

            foreach (GameEntity product in _productBuffer)
            {
                product.RemoveOrderLineEntityId();
                product.isDestructed = true;
            }
            foreach (GameEntity line in _lineBuffer)
            {
                line.RemoveOrderEntityId();
                line.isDestructed = true;
            }

            if (_gameContext.GetEntitiesWithOrderEntityId(visit.EntityId).Count != 0)
                throw new InvalidOperationException(
                    $"Departed customer visit {visit.EntityId} retained an order-line relation.");
            foreach (GameEntity line in _lineBuffer)
            {
                if (_gameContext.GetEntitiesWithOrderLineEntityId(line.EntityId).Count != 0)
                    throw new InvalidOperationException(
                        $"Departed order line {line.EntityId} retained a product relation.");
                if (_gameContext
                        .GetEntitiesWithReservedOrderLineEntityId(line.EntityId).Count != 0)
                    throw new InvalidOperationException(
                        $"Departed order line {line.EntityId} retained a product reservation.");
            }

            visit.isOrderContentReleased = true;
        }

        private int CollectOrderLines(GameEntity visit)
        {
            _lineBuffer.Clear();
            int requiredProductCount = 0;
            foreach (GameEntity line in
                     _gameContext.GetEntitiesWithOrderEntityId(visit.EntityId))
            {
                if (!line.isOrderLine || line.isDestructed || !line.hasEntityId ||
                    !line.hasOrderEntityId || !line.hasProductType ||
                    !line.hasRequiredProductCount || !line.hasLoadedProductCount ||
                    line.OrderEntityId != visit.EntityId ||
                    line.RequiredProductCount <= 0 ||
                    line.LoadedProductCount != line.RequiredProductCount)
                {
                    throw new InvalidOperationException(
                        $"Departed customer visit {visit.EntityId} has an invalid order line.");
                }
                requiredProductCount = checked(
                    requiredProductCount + line.RequiredProductCount);
                _lineBuffer.Add(line);
            }

            if (_lineBuffer.Count == 0)
                throw new InvalidOperationException(
                    $"Departed customer visit {visit.EntityId} has no order lines.");
            return requiredProductCount;
        }

        private int CollectLoadedProducts(GameEntity visit)
        {
            _productBuffer.Clear();
            int loadedProductCount = 0;
            foreach (GameEntity line in _lineBuffer)
            {
                int lineProductCount = 0;
                foreach (GameEntity product in
                         _gameContext.GetEntitiesWithOrderLineEntityId(line.EntityId))
                {
                    if (!product.isProduct || !product.isLoaded || product.isProductLoaded ||
                        product.isDestructed || !product.hasEntityId ||
                        !product.hasOrderLineEntityId || !product.hasProductType ||
                        !product.hasLoadingSlotIndex ||
                        product.OrderLineEntityId != line.EntityId ||
                        product.ProductType != line.ProductType)
                    {
                        throw new InvalidOperationException(
                            $"Customer visit {visit.EntityId} order line {line.EntityId} " +
                            "has an invalid linked product.");
                    }

                    lineProductCount++;
                    _productBuffer.Add(product);
                }

                if (lineProductCount != line.LoadedProductCount)
                {
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} order line {line.EntityId} contains " +
                        $"{lineProductCount} products, but reports " +
                        $"{line.LoadedProductCount} loaded.");
                }

                loadedProductCount = checked(loadedProductCount + lineProductCount);
            }

            return loadedProductCount;
        }
    }
}
