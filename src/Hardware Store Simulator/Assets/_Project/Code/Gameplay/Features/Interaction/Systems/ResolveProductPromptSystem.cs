using System;
using System.Linq;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveProductPromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IStaticDataService _staticData;
        private readonly IGroup<GameEntity> _players;

        public ResolveProductPromptSystem(GameContext gameContext,
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
                if (player.FocusedInteractionType != InteractionTypeId.Product)
                    continue;

                GameEntity product =
                    _gameContext.GetEntityWithEntityId(player.FocusedEntityId);
                ProductConfig productConfig = _staticData.GetProduct(product.ProductType);
                GameEntity store =
                    _gameContext.GetEntityWithEntityId(player.StoreEntityId);
                if (product.isInboundProduct)
                {
                    GameEntity terminal = _gameContext.GetEntityWithEntityId(
                        store.ProcurementTerminalEntityId);
                    GameEntity delivery =
                        _gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                            terminal.EntityId);
                    if (delivery == null || delivery.EntityId != product.DeliveryEntityId)
                        continue;
                }
                else if (product.isInStock)
                {
                    if (product.StorageZoneEntityId != store.StorageZoneEntityId)
                        continue;
                }

                if (product.isLoaded)
                {
                    player.SetInteractionPrompt(
                        $"Товар уже загружен клиенту • товар: " +
                        $"{productConfig.DisplayName}",
                        false);
                    continue;
                }

                if (player.isHandsOccupied)
                {
                    player.SetInteractionPrompt(
                        "Руки заняты • G — бросить предмет",
                        false);
                    continue;
                }

                if (product.isInboundProduct)
                {
                    player.SetInteractionPrompt(
                        $"E — взять из поставки • товар: {productConfig.DisplayName}",
                        true);
                    continue;
                }

                if (!product.isInStock)
                {
                    player.SetInteractionPrompt(
                        "Этот товар сейчас нельзя взять",
                        false);
                    continue;
                }

                GameEntity customerVisit =
                    _gameContext.GetEntityWithCustomerVisitStoreEntityId(store.EntityId);
                if (customerVisit == null)
                {
                    player.SetInteractionPrompt(
                        "Ожидайте следующего клиента — товар пока не требуется",
                        false);
                    continue;
                }
                if (customerVisit.isCustomerVisitArriving)
                {
                    player.SetInteractionPrompt(
                        "Клиент прибывает и направляется к стойке — дождитесь консультации",
                        false);
                    continue;
                }

                if (customerVisit.isCustomerVisitReturning ||
                    customerVisit.isCustomerVisitDeparting)
                {
                    player.SetInteractionPrompt(
                        customerVisit.isCustomerVisitReturning
                            ? "Клиент возвращается к машине — ожидайте следующего"
                            : "Машина клиента уезжает — ожидайте следующего",
                        false);
                    continue;
                }

                if (customerVisit.isCustomerVisitConsulting)
                {
                    player.SetInteractionPrompt(
                        "Сначала согласуйте предложение с клиентом у стойки",
                        false);
                    continue;
                }

                if (customerVisit.isCustomerVisitWaiting)
                {
                    player.SetInteractionPrompt(
                        "Сначала примите заказ у стойки",
                        false);
                    continue;
                }

                if (customerVisit.isCustomerVisitCompleted)
                {
                    player.SetInteractionPrompt("Заказ уже выполнен", false);
                    continue;
                }

                if (!customerVisit.isCustomerVisitLoading)
                    throw new InvalidOperationException(
                        $"Customer visit {customerVisit.EntityId} has no valid lifecycle state.");

                GameEntity[] lines = GetOrderLines(customerVisit);
                GameEntity matchingLine = lines.FirstOrDefault(line =>
                    line.ProductType == product.ProductType);
                bool available = matchingLine != null &&
                                 matchingLine.LoadedProductCount <
                                 matchingLine.RequiredProductCount;
                player.SetInteractionPrompt(
                    available
                        ? $"E — взять со склада • товар: {productConfig.DisplayName}"
                        : matchingLine == null
                            ? $"Товар не входит в заказ: {productConfig.DisplayName}"
                            : $"Позиция уже загружена полностью: " +
                              $"{productConfig.DisplayName}",
                    available);
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
