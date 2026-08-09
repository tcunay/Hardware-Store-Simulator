using System;
using Entitas;
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

                GameEntity orderCounter =
                    _gameContext.GetEntityWithEntityId(player.FocusedEntityId);
                if (orderCounter.StoreEntityId != player.StoreEntityId)
                    continue;

                GameEntity customerVisit =
                    _gameContext.GetEntityWithCustomerVisitStoreEntityId(
                        orderCounter.StoreEntityId);
                if (customerVisit == null)
                {
                    player.SetInteractionPrompt("Ожидаем следующего клиента", false);
                    continue;
                }
                if (customerVisit.isCustomerVisitArriving)
                {
                    player.SetInteractionPrompt("Клиент подъезжает", false);
                    continue;
                }

                if (customerVisit.isCustomerVisitDeparting)
                {
                    player.SetInteractionPrompt("Клиент уезжает", false);
                    continue;
                }

                if (customerVisit.isCustomerVisitWaiting)
                {
                    bool available =
                        customerVisit.AvailableProductCount >=
                        customerVisit.RequiredProductCount;
                    player.SetInteractionPrompt(
                        available
                            ? $"E — принять заказ на {customerVisit.RequiredProductCount} мешка цемента"
                            : $"Сначала закупите товар: на складе " +
                              $"{customerVisit.AvailableProductCount}/" +
                              $"{customerVisit.RequiredProductCount}",
                        available);
                    continue;
                }

                if (customerVisit.isCustomerVisitLoading)
                {
                    player.SetInteractionPrompt(
                        "Заказ принят — загрузите товар в машину клиента",
                        false);
                    continue;
                }

                if (!customerVisit.isCustomerVisitCompleted)
                    throw new InvalidOperationException(
                        $"Customer visit {customerVisit.EntityId} has no valid lifecycle state.");

                player.SetInteractionPrompt(
                    "Заказ выполнен — клиент готовится уезжать",
                    false);
            }
        }
    }
}
