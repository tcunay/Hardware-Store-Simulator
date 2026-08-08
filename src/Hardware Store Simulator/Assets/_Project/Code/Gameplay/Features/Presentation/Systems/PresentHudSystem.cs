using System;
using Entitas;
using HardwareStore.Gameplay.Presentation;

namespace HardwareStore.Gameplay.Features.Presentation.Systems
{
    public sealed class PresentHudSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IHudService _hud;
        private readonly IGroup<GameEntity> _players;

        public PresentHudSystem(GameContext gameContext, IHudService hud)
        {
            _gameContext = gameContext;
            _hud = hud;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.OrderEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                GameEntity order = _gameContext.GetEntityWithEntityId(player.OrderEntityId);
                if (!order.isOrder)
                    throw new InvalidOperationException($"Entity {player.OrderEntityId} is not an order.");

                GameEntity wallet = _gameContext.GetEntityWithEntityId(order.WalletEntityId);
                if (!wallet.isWallet)
                    throw new InvalidOperationException($"Entity {order.WalletEntityId} is not a wallet.");

                _hud.Present(new HudSnapshot(
                    ResolveOrderState(order),
                    order.LoadedProductCount,
                    order.RequiredProductCount,
                    wallet.Money,
                    player.hasInteractionPrompt ? player.InteractionPrompt : string.Empty,
                    player.hasFocusedEntityId,
                    player.isFocusInteractionAvailable,
                    player.hasHeldProductId,
                    player.isCursorLocked));
            }
        }

        private static HudOrderState ResolveOrderState(GameEntity order)
        {
            if (order.isOrderWaiting)
                return HudOrderState.Waiting;
            if (order.isOrderActive)
                return HudOrderState.Active;
            if (order.isOrderCompleted)
                return HudOrderState.Completed;
            throw new InvalidOperationException("Order entity has no state tag.");
        }
    }
}
