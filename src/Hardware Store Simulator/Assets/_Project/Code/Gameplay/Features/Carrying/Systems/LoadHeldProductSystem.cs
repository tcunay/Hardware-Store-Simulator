using System;
using Entitas;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Carrying.Systems
{
    public sealed class LoadHeldProductSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public LoadHeldProductSystem(GameContext gameContext, IGameEventFactory events)
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
                GameEntity loadingZone = _gameContext.GetEntityWithEntityId(request.TargetEntityId);
                if (!loadingZone.isLoadingZone)
                    continue;

                GameEntity player = _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                if (!player.isPlayer)
                    throw new InvalidOperationException($"Interaction source {request.SourceEntityId} is not a player.");
                if (!player.hasHeldProductId || loadingZone.OrderEntityId != player.OrderEntityId)
                    continue;

                GameEntity order = _gameContext.GetEntityWithEntityId(player.OrderEntityId);
                if (!order.isOrder)
                    throw new InvalidOperationException($"Entity {player.OrderEntityId} is not an order.");
                if (!order.isOrderActive || order.LoadedProductCount >= order.RequiredProductCount)
                    continue;

                int productId = player.HeldProductId;
                GameEntity product = _gameContext.GetEntityWithEntityId(productId);
                if (!product.isProduct || !product.isCarried || product.isLoaded ||
                    product.ProductType != order.RequiredProductType)
                    continue;

                player.RemoveHeldProductId();
                product.isCarried = false;
                product.isLoaded = true;
                product.ProductView.SnapToLoadingSlot(loadingZone.LoadingSlots[order.LoadedProductCount]);
                _events.EmitProductLoaded(productId, order.EntityId);
            }
        }
    }
}
