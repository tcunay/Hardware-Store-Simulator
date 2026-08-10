using System;
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
                        "Клиент подъезжает — можно подготовить товар",
                        false);
                    continue;
                }

                if (customerVisit.isCustomerVisitDeparting)
                {
                    player.SetInteractionPrompt(
                        "Клиент уезжает — ожидайте следующего",
                        false);
                    continue;
                }

                if (customerVisit.isCustomerVisitConsulting)
                {
                    ProductConfig requestedProduct =
                        _staticData.GetProduct(customerVisit.RequestedProductType);
                    player.SetInteractionPrompt(
                        $"Сначала согласуйте предложение • запрос: " +
                        $"{requestedProduct.DisplayName}",
                        false);
                    continue;
                }

                if (customerVisit.isCustomerVisitLoading)
                {
                    ProductConfig product =
                        _staticData.GetProduct(customerVisit.RequiredProductType);
                    player.SetInteractionPrompt(
                        customerVisit.AvailableProductCount > 0
                            ? $"Наведитесь на товар: {product.DisplayName} • E — взять"
                            : $"Нет товара для заказа: {product.DisplayName} • " +
                              "закажите поставку",
                        false);
                    continue;
                }

                if (customerVisit.isCustomerVisitWaiting)
                {
                    ProductConfig product =
                        _staticData.GetProduct(customerVisit.RequiredProductType);
                    if (customerVisit.AvailableProductCount >=
                        customerVisit.RequiredProductCount)
                    {
                        player.SetInteractionPrompt(
                            $"Товар готов: {product.DisplayName} • примите заказ у стойки",
                            false);
                    }
                    else if (customerVisit.AvailableProductCount > 0)
                    {
                        player.SetInteractionPrompt(
                            $"Нужен товар: {product.DisplayName} • доступно: " +
                            $"{customerVisit.AvailableProductCount}/" +
                            $"{customerVisit.RequiredProductCount} {product.UnitLabel} • " +
                            "закажите поставку",
                            false);
                    }
                    else
                    {
                        player.SetInteractionPrompt(
                            $"Нет товара для заказа: {product.DisplayName} • " +
                            "закажите поставку",
                            false);
                    }

                    continue;
                }

                if (!customerVisit.isCustomerVisitCompleted)
                    throw new InvalidOperationException(
                        $"Customer visit {customerVisit.EntityId} has no valid lifecycle state.");

                player.SetInteractionPrompt(
                    customerVisit.AvailableProductCount > 0
                        ? $"Заказ выполнен • осталось нужного товара: " +
                          $"{customerVisit.AvailableProductCount}"
                        : "Склад пуст — закажите поставку в терминале закупок",
                    false);
            }
        }
    }
}
