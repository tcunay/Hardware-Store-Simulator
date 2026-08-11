using System;
using System.Linq;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveLoadingZonePromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IStaticDataService _staticData;
        private readonly IGroup<GameEntity> _players;

        public ResolveLoadingZonePromptSystem(GameContext gameContext,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _staticData = staticData;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.EntityId,
                GameMatcher.StoreEntityId,
                GameMatcher.FocusedEntityId,
                GameMatcher.FocusedInteractionType));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                if (player.FocusedInteractionType != InteractionTypeId.LoadingZone)
                    continue;

                GameEntity loadingZone =
                    _gameContext.GetEntityWithEntityId(player.FocusedEntityId);
                if (loadingZone.CustomerVisitStoreEntityId != player.StoreEntityId)
                    continue;
                if (loadingZone.isCustomerVisitArriving)
                {
                    player.SetInteractionPrompt(
                        "Клиент прибывает и направляется к стойке — дождитесь консультации",
                        false);
                    continue;
                }

                if (loadingZone.isCustomerVisitDeparting)
                {
                    player.SetInteractionPrompt(
                        "Машина клиента уезжает — загрузка завершена",
                        false);
                    continue;
                }

                if (loadingZone.isCustomerVisitReturning)
                {
                    player.SetInteractionPrompt(
                        "Клиент возвращается к машине — загрузка завершена",
                        false);
                    continue;
                }

                if (loadingZone.isCustomerVisitConsulting)
                {
                    player.SetInteractionPrompt(
                        "Сначала согласуйте предложение с клиентом у стойки",
                        false);
                    continue;
                }

                if (loadingZone.isCustomerVisitWaiting)
                {
                    player.SetInteractionPrompt(
                        "Сначала примите заказ у стойки",
                        false);
                    continue;
                }

                if (loadingZone.isCustomerVisitCompleted)
                {
                    player.SetInteractionPrompt(
                        "Машина загружена — заказ выполнен",
                        false);
                    continue;
                }

                if (!loadingZone.isCustomerVisitLoading)
                    throw new InvalidOperationException(
                        $"Customer visit {loadingZone.EntityId} has no valid lifecycle state.");

                GameEntity[] lines = GetOrderLines(loadingZone);
                if (!player.isHandsOccupied)
                {
                    GameEntity incompleteLine = lines.FirstOrDefault(line =>
                        line.LoadedProductCount < line.RequiredProductCount);
                    if (incompleteLine == null)
                    {
                        player.SetInteractionPrompt(
                            "Все позиции загружены — заказ завершается",
                            false);
                        continue;
                    }

                    ProductConfig requiredProduct =
                        _staticData.GetProduct(incompleteLine.ProductType);
                    player.SetInteractionPrompt(
                        $"Принесите со склада: {requiredProduct.DisplayName} • " +
                        $"загружено {incompleteLine.LoadedProductCount}/" +
                        $"{incompleteLine.RequiredProductCount}",
                        false);
                    continue;
                }

                GameEntity heldProduct =
                    _gameContext.GetEntityWithCarrierEntityId(player.EntityId);
                GameEntity matchingLine = lines.FirstOrDefault(line =>
                    line.ProductType == heldProduct.ProductType);

                bool available = heldProduct.isInStock &&
                                 heldProduct.StorageZoneEntityId ==
                                 loadingZone.StorageZoneEntityId &&
                                 matchingLine != null &&
                                 matchingLine.LoadedProductCount <
                                 matchingLine.RequiredProductCount;
                if (!available)
                {
                    ProductConfig heldProductConfig =
                        _staticData.GetProduct(heldProduct.ProductType);
                    player.SetInteractionPrompt(
                        matchingLine == null
                            ? $"Товар не входит в заказ: {heldProductConfig.DisplayName}"
                            : matchingLine.LoadedProductCount >=
                              matchingLine.RequiredProductCount
                                ? $"Позиция уже загружена: " +
                                  $"{heldProductConfig.DisplayName}"
                                : $"Товар нужно взять со склада: " +
                                  $"{heldProductConfig.DisplayName}",
                        false);
                    continue;
                }

                int loadedUnitCount = lines.Sum(line => line.LoadedProductCount);
                if (loadingZone.Slots.Length <= loadedUnitCount)
                {
                    player.SetInteractionPrompt(
                        "В машине клиента нет свободного места",
                        false);
                    continue;
                }

                ProductConfig product = _staticData.GetProduct(heldProduct.ProductType);
                player.SetInteractionPrompt(
                    $"E — загрузить • {product.DisplayName} • " +
                    $"{matchingLine.LoadedProductCount}/" +
                    $"{matchingLine.RequiredProductCount}",
                    true);
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
