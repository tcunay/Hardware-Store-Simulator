using System;
using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveOrderCounterPromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;

        public ResolveOrderCounterPromptSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.StoreEntityId,
                GameMatcher.FocusedEntityId,
                GameMatcher.FocusedInteractionType));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                if (player.FocusedInteractionType != InteractionTypeId.OrderCounter)
                    continue;

                GameEntity orderCounter = _gameContext.GetRequiredEntity(
                    player.FocusedEntityId,
                    "focused order counter");
                if (!orderCounter.isOrderCounter || !orderCounter.hasOrderEntityId)
                    throw new InvalidOperationException(
                        $"Entity {orderCounter.EntityId} is not a configured order counter.");

                GameEntity store = _gameContext.RequireStore(player);
                if (orderCounter.OrderEntityId != store.OrderEntityId)
                    continue;

                GameEntity order = _gameContext.GetRequiredEntity(
                    store.OrderEntityId,
                    "store order");
                if (!order.isOrder ||
                    order.StoreEntityId != store.EntityId ||
                    order.StorageZoneEntityId != store.StorageZoneEntityId)
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} has an invalid order relation.");

                if (order.isOrderWaiting)
                {
                    bool available =
                        order.AvailableProductCount >= order.RequiredProductCount;
                    player.SetInteractionPrompt(
                        available
                            ? $"E — принять заказ на {order.RequiredProductCount} мешка цемента"
                            : $"Сначала закупите товар: на складе " +
                              $"{order.AvailableProductCount}/{order.RequiredProductCount}",
                        available);
                    continue;
                }

                if (order.isOrderActive)
                {
                    player.SetInteractionPrompt(
                        "Заказ принят — загрузите товар в машину клиента",
                        false);
                    continue;
                }

                if (!order.isOrderCompleted)
                    throw new InvalidOperationException(
                        $"Order {order.EntityId} has no valid lifecycle state.");

                player.SetInteractionPrompt(
                    "Заказ выполнен — деньги получены",
                    false);
            }
        }
    }
}
