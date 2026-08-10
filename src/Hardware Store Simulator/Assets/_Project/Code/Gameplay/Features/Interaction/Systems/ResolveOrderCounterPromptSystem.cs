using System;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveOrderCounterPromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IStaticDataService _staticData;
        private readonly IGroup<GameEntity> _players;

        public ResolveOrderCounterPromptSystem(GameContext gameContext,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _staticData = staticData;
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
                    ProductConfig product =
                        _staticData.GetProduct(customerVisit.RequiredProductType);
                    bool available =
                        customerVisit.AvailableProductCount >=
                        customerVisit.RequiredProductCount;
                    player.SetInteractionPrompt(
                        available
                            ? $"E — принять заказ • товар: {product.DisplayName} • " +
                              $"количество: {customerVisit.RequiredProductCount} " +
                              $"{product.UnitLabel}"
                            : $"Нужен товар: {product.DisplayName} • доступно: " +
                              $"{customerVisit.AvailableProductCount}/" +
                              $"{customerVisit.RequiredProductCount} {product.UnitLabel}",
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
