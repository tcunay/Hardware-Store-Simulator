using System;
using Entitas;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveInteractionPromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<GameEntity> _focusedPlayers;

        public ResolveInteractionPromptSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _players = gameContext.GetGroup(GameMatcher.AllOf(GameMatcher.Player));
            _focusedPlayers = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.OrderEntityId,
                GameMatcher.FocusedEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                player.isFocusInteractionAvailable = false;
                if (player.hasInteractionPrompt)
                    player.RemoveInteractionPrompt();
            }

            foreach (GameEntity player in _focusedPlayers)
            {
                GameEntity target = _gameContext.GetEntityWithEntityId(player.FocusedEntityId);
                if ((target.isOrderCounter || target.isLoadingZone) &&
                    target.OrderEntityId != player.OrderEntityId)
                    continue;

                GameEntity order = _gameContext.GetEntityWithEntityId(player.OrderEntityId);
                if (!order.isOrder)
                    throw new InvalidOperationException($"Entity {player.OrderEntityId} is not an order.");

                (string prompt, bool available) = Resolve(target, player, order);
                player.ReplaceInteractionPrompt(prompt);
                player.isFocusInteractionAvailable = available;
            }
        }

        private (string Prompt, bool Available) Resolve(GameEntity target, GameEntity player,
            GameEntity order)
        {
            if (target.isOrderCounter)
            {
                if (order.isOrderWaiting)
                    return ($"E — принять заказ на {order.RequiredProductCount} мешков цемента", true);
                if (order.isOrderActive)
                    return ("Заказ принят — товар ждёт на палете", false);
                return ("Заказ выполнен — деньги получены", false);
            }

            if (target.isProduct)
            {
                if (order.isOrderWaiting)
                    return ("Сначала примите заказ у стойки", false);
                if (order.isOrderCompleted)
                    return ("Заказ уже выполнен", false);
                if (player.hasHeldProductId)
                    return ("Руки заняты — G, чтобы бросить мешок", false);
                bool available = !target.isCarried && !target.isLoaded &&
                                 target.ProductType == order.RequiredProductType;
                return (available ? "E — взять мешок цемента" : "Этот товар нельзя взять", available);
            }

            if (target.isLoadingZone)
            {
                if (order.isOrderWaiting)
                    return ("Сначала примите заказ у стойки", false);
                if (order.isOrderCompleted)
                    return ("Машина загружена — заказ выполнен", false);
                if (!player.hasHeldProductId)
                    return ("Принесите сюда мешок цемента", false);

                GameEntity heldProduct = _gameContext.GetEntityWithEntityId(player.HeldProductId);
                bool available = heldProduct.ProductType == order.RequiredProductType;
                return available
                    ? ("E — положить мешок в кузов", true)
                    : ("Для этого заказа нужен другой товар", false);
            }

            throw new InvalidOperationException($"Entity {target.EntityId} is interactable without a supported role.");
        }
    }
}
