using System;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Orders.Systems
{
    public sealed class RegisterLoadedProductSystem : IExecuteSystem
    {
        private readonly IGameEventFactory _events;
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _loadedEvents;

        public RegisterLoadedProductSystem(GameContext gameContext, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _loadedEvents = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.ProductLoaded,
                GameMatcher.ProductEntityId,
                GameMatcher.OrderEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity loadedEvent in _loadedEvents)
            {
                GameEntity order = _gameContext.GetEntityWithEntityId(loadedEvent.OrderEntityId);
                if (!order.isOrder || !order.isOrderActive)
                    throw new InvalidOperationException("A product can only be registered for an active order.");

                GameEntity product = _gameContext.GetEntityWithEntityId(loadedEvent.ProductEntityId);
                if (!product.isProduct || !product.isLoaded || product.ProductType != order.RequiredProductType)
                    throw new InvalidOperationException("The loaded product does not satisfy the order.");

                int loaded = order.LoadedProductCount;
                int required = order.RequiredProductCount;
                if (loaded >= required)
                    throw new InvalidOperationException("The order already contains all required products.");

                loaded++;
                order.ReplaceLoadedProductCount(loaded);
                if (loaded < required)
                {
                    _events.EmitNotification($"Мешок загружен: {loaded}/{required}");
                    _events.EmitAudio(AudioCueId.Load);
                }
            }
        }
    }
}
