using System;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Carrying.Systems
{
    public sealed class PickUpProductSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public PickUpProductSystem(GameContext gameContext, IGameEventFactory events)
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
                GameEntity product = _gameContext.GetEntityWithEntityId(request.TargetEntityId);
                if (!product.isProduct)
                    continue;

                GameEntity player = _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                if (!player.isPlayer)
                    throw new InvalidOperationException($"Interaction source {request.SourceEntityId} is not a player.");
                if (player.hasHeldProductId)
                    continue;

                GameEntity order = _gameContext.GetEntityWithEntityId(player.OrderEntityId);
                if (!order.isOrder)
                    throw new InvalidOperationException($"Entity {player.OrderEntityId} is not an order.");
                if (!order.isOrderActive || product.isCarried || product.isLoaded ||
                    product.ProductType != order.RequiredProductType)
                    continue;

                int productId = product.EntityId;
                player.AddHeldProductId(productId);
                product.isCarried = true;
                product.ProductView.AttachToHands(player.CarryAnchor);
                _events.EmitAudio(AudioCueId.PickUp);
            }
        }
    }
}
