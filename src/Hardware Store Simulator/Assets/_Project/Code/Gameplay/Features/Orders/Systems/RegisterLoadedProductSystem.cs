using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Orders.Systems
{
    public sealed class RegisterLoadedProductSystem : IExecuteSystem
    {
        private readonly IGameEventFactory _events;
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _loadedProducts;
        private readonly List<GameEntity> _buffer = new(8);

        public RegisterLoadedProductSystem(GameContext gameContext, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _loadedProducts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.EntityId,
                    GameMatcher.ProductLoaded,
                    GameMatcher.Loaded,
                    GameMatcher.CustomerVisitEntityId,
                    GameMatcher.ProductType)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity product in _loadedProducts.GetEntities(_buffer))
            {
                GameEntity visit = _gameContext.GetEntityWithEntityId(
                    product.CustomerVisitEntityId);
                if (!visit.isCustomerVisitLoading ||
                    product.ProductType != visit.RequiredProductType)
                {
                    throw new InvalidOperationException(
                        $"Product {product.EntityId} cannot be registered for customer visit " +
                        $"{visit.EntityId}.");
                }

                int loaded = visit.LoadedProductCount;
                int required = visit.RequiredProductCount;
                if (loaded >= required)
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} already contains all required products.");

                loaded++;
                visit.ReplaceLoadedProductCount(loaded);
                product.isProductLoaded = false;
                if (loaded < required)
                {
                    _events.EmitNotification($"Мешок загружен: {loaded}/{required}");
                    _events.EmitAudio(AudioCueId.Load);
                }
            }
        }
    }
}
