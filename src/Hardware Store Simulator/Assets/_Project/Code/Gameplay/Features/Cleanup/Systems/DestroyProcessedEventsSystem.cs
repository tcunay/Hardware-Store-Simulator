using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Cleanup.Systems
{
    public sealed class DestroyProcessedEventsSystem : ICleanupSystem
    {
        private readonly IGroup<GameEntity> _interactionRequests;
        private readonly IGroup<GameEntity> _purchaseDeliveryRequests;
        private readonly List<GameEntity> _buffer = new(16);

        public DestroyProcessedEventsSystem(GameContext gameContext)
        {
            _interactionRequests = gameContext.GetGroup(GameMatcher.AllOf(GameMatcher.InteractionRequest));
            _purchaseDeliveryRequests = gameContext.GetGroup(
                GameMatcher.AllOf(GameMatcher.PurchaseDeliveryRequest));
        }

        public void Cleanup()
        {
            Destroy(_interactionRequests);
            Destroy(_purchaseDeliveryRequests);
        }

        private void Destroy(IGroup<GameEntity> group)
        {
            foreach (GameEntity entity in group.GetEntities(_buffer))
                entity.Destroy();
        }
    }
}
