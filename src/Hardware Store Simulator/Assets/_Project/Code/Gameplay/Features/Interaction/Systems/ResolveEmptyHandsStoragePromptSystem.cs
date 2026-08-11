using System;
using System.Linq;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveEmptyHandsStoragePromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IStaticDataService _staticData;
        private readonly IGroup<GameEntity> _players;

        public ResolveEmptyHandsStoragePromptSystem(GameContext gameContext,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _staticData = staticData;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Player,
                    GameMatcher.StoreEntityId,
                    GameMatcher.FocusedEntityId,
                    GameMatcher.FocusedInteractionType)
                .NoneOf(GameMatcher.HandsOccupied));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                if (player.FocusedInteractionType != InteractionTypeId.StorageZone)
                    continue;

                GameEntity storageZone =
                    _gameContext.GetEntityWithEntityId(player.FocusedEntityId);
                GameEntity store =
                    _gameContext.GetEntityWithEntityId(player.StoreEntityId);
                if (storageZone.EntityId != store.StorageZoneEntityId)
                    continue;
                GameEntity customerVisit =
                    _gameContext.GetEntityWithCustomerVisitStoreEntityId(store.EntityId);

                GameEntity terminal = _gameContext.GetEntityWithEntityId(
                    store.ProcurementTerminalEntityId);
                GameEntity delivery =
                    _gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                        terminal.EntityId);
                if (delivery != null)
                {
                    ProductConfig deliveredProduct =
                        _staticData.GetProduct(delivery.ProductType);
                    player.SetInteractionPrompt(
                        $"Принесите товар из поставки на приёмку • товар: " +
                        $"{deliveredProduct.DisplayName}",
                        false);
                    continue;
                }

                if (customerVisit == null)
                {
                    player.SetInteractionPrompt(
                        storageZone.StorageProductCount > 0
                            ? $"Товаров на складе: {storageZone.StorageProductCount} • " +
                              "Ожидайте следующего клиента"
                            : "Склад пуст — можно заказать поставку",
                        false);
                    continue;
                }
                if (customerVisit.isCustomerVisitArriving)
                {
                    player.SetInteractionPrompt(
                        "Клиент прибывает и направляется к стойке — можно подготовить товар",
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
                        $"Сначала согласуйте предложение • проект: " +
                        $"{customerVisit.CustomerProjectTitle}",
                        false);
                    continue;
                }

                if (customerVisit.isCustomerVisitLoading)
                {
                    GameEntity[] lines = GetOrderLines(customerVisit);
                    GameEntity incompleteLine = lines.FirstOrDefault(line =>
                        line.LoadedProductCount < line.RequiredProductCount);
                    if (incompleteLine == null)
                    {
                        player.SetInteractionPrompt(
                            "Все позиции загружены — заказ завершается",
                            false);
                        continue;
                    }

                    GameEntity availableLine = lines.FirstOrDefault(line =>
                        line.LoadedProductCount < line.RequiredProductCount &&
                        line.AvailableProductCount > 0);
                    GameEntity promptedLine = availableLine ?? incompleteLine;
                    ProductConfig product = _staticData.GetProduct(promptedLine.ProductType);
                    player.SetInteractionPrompt(
                        availableLine != null
                            ? $"Наведитесь на товар: {product.DisplayName} • E — взять"
                            : $"Нет товара для заказа: {product.DisplayName} • " +
                              "закажите поставку",
                        false);
                    continue;
                }

                if (customerVisit.isCustomerVisitWaiting)
                {
                    GameEntity[] lines = GetOrderLines(customerVisit);
                    GameEntity deficitLine = lines.FirstOrDefault(line =>
                        line.AvailableProductCount < line.RequiredProductCount);
                    if (deficitLine == null)
                    {
                        player.SetInteractionPrompt(
                            "Все позиции готовы • примите заказ у стойки",
                            false);
                    }
                    else
                    {
                        ProductConfig product =
                            _staticData.GetProduct(deficitLine.ProductType);
                        player.SetInteractionPrompt(
                            $"Нужен товар: {product.DisplayName} • доступно: " +
                            $"{deficitLine.AvailableProductCount}/" +
                            $"{deficitLine.RequiredProductCount} {product.UnitLabel} • " +
                            "закажите поставку",
                            false);
                    }

                    continue;
                }

                if (!customerVisit.isCustomerVisitCompleted)
                    throw new InvalidOperationException(
                        $"Customer visit {customerVisit.EntityId} has no valid lifecycle state.");

                player.SetInteractionPrompt(
                    storageZone.StorageProductCount > 0
                        ? $"Заказ выполнен • товаров на складе: " +
                          $"{storageZone.StorageProductCount}"
                        : "Заказ выполнен • склад пуст",
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
