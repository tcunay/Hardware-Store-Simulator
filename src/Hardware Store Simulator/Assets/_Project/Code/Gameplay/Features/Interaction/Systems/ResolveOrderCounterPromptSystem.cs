using System;
using System.Linq;
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
                    player.SetInteractionPrompt("Клиент направляется к стойке", false);
                    continue;
                }

                if (customerVisit.isCustomerVisitDeparting)
                {
                    player.SetInteractionPrompt("Машина клиента уезжает", false);
                    continue;
                }

                if (customerVisit.isCustomerVisitReturning)
                {
                    player.SetInteractionPrompt("Клиент возвращается к машине", false);
                    continue;
                }

                if (customerVisit.isCustomerVisitConsulting)
                {
                    player.SetInteractionPrompt(
                        player.isHandsOccupied
                            ? "Освободите руки перед консультацией"
                            : $"E — обсудить проект • {customerVisit.CustomerProjectTitle}",
                        !player.isHandsOccupied);
                    continue;
                }

                if (customerVisit.isCustomerVisitWaiting)
                {
                    GameEntity[] lines = GetOrderLines(customerVisit);
                    GameEntity deficitLine = lines.FirstOrDefault(line =>
                        line.AvailableProductCount < line.RequiredProductCount);
                    if (deficitLine == null)
                    {
                        int totalUnitCount = lines.Sum(line => line.RequiredProductCount);
                        player.SetInteractionPrompt(
                            $"E — принять заказ • {lines.Length} поз. • " +
                            $"{totalUnitCount} ед.",
                            true);
                        continue;
                    }

                    ProductConfig product = _staticData.GetProduct(deficitLine.ProductType);
                    player.SetInteractionPrompt(
                        $"Не хватает: {product.DisplayName} • склад " +
                        $"{deficitLine.AvailableProductCount}/нужно " +
                        $"{deficitLine.RequiredProductCount} {product.UnitLabel}",
                        false);
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

        private GameEntity[] GetOrderLines(GameEntity order)
        {
            GameEntity[] lines = _gameContext
                .GetEntitiesWithOrderEntityId(order.EntityId)
                .Where(line => line.isOrderLine && !line.isDestructed)
                .OrderBy(line => line.LineIndex)
                .ToArray();
            if (lines.Length == 0)
                throw new InvalidOperationException(
                    $"Order {order.EntityId} has no active product lines.");

            return lines;
        }
    }
}
