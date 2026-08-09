using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Cleanup.Systems
{
    public sealed class DestroyProcessedEventsSystem : ICleanupSystem
    {
        private readonly IGroup<GameEntity> _interactionRequests;
        private readonly IGroup<GameEntity> _productLoadedEvents;
        private readonly IGroup<GameEntity> _productStockedEvents;
        private readonly IGroup<GameEntity> _orderCompletedEvents;
        private readonly List<GameEntity> _buffer = new(16);

        public DestroyProcessedEventsSystem(GameContext gameContext)
        {
            _interactionRequests = gameContext.GetGroup(GameMatcher.AllOf(GameMatcher.InteractionRequest));
            _productLoadedEvents = gameContext.GetGroup(GameMatcher.AllOf(GameMatcher.ProductLoaded));
            _productStockedEvents = gameContext.GetGroup(GameMatcher.AllOf(GameMatcher.ProductStocked));
            _orderCompletedEvents = gameContext.GetGroup(GameMatcher.AllOf(GameMatcher.OrderCompletedEvent));
        }

        public void Cleanup()
        {
            Destroy(_interactionRequests);
            Destroy(_productLoadedEvents);
            Destroy(_productStockedEvents);
            Destroy(_orderCompletedEvents);
        }

        private void Destroy(IGroup<GameEntity> group)
        {
            foreach (GameEntity entity in group.GetEntities(_buffer))
                entity.Destroy();
        }
    }
}
