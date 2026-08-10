using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Orders.Systems
{
    public sealed class RegisterLoadedProductSystem : IExecuteSystem
    {
        private readonly IGameEventFactory _events;
        private readonly IStaticDataService _staticData;
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _loadedProducts;
        private readonly List<GameEntity> _buffer = new(8);

        public RegisterLoadedProductSystem(GameContext gameContext,
            IStaticDataService staticData, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _staticData = staticData;
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
                    ProductConfig productConfig =
                        _staticData.GetProduct(product.ProductType);
                    _events.EmitNotification(
                        $"Товар загружен • {productConfig.DisplayName}: " +
                        $"{loaded}/{required} {productConfig.UnitLabel}");
                    _events.EmitAudio(AudioCueId.Load);
                }
            }
        }
    }
}
