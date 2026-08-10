using System;
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
                        "Клиент подъезжает — дождитесь остановки машины",
                        false);
                    continue;
                }

                if (loadingZone.isCustomerVisitDeparting)
                {
                    player.SetInteractionPrompt(
                        "Клиент уезжает — загрузка завершена",
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

                if (!player.isHandsOccupied)
                {
                    ProductConfig requiredProduct =
                        _staticData.GetProduct(loadingZone.RequiredProductType);
                    player.SetInteractionPrompt(
                        $"Принесите товар со склада • товар: " +
                        $"{requiredProduct.DisplayName}",
                        false);
                    continue;
                }

                GameEntity heldProduct =
                    _gameContext.GetEntityWithCarrierEntityId(player.EntityId);

                bool available = heldProduct.isInStock &&
                                 heldProduct.StorageZoneEntityId ==
                                 loadingZone.StorageZoneEntityId &&
                                 heldProduct.ProductType == loadingZone.RequiredProductType;
                if (!available)
                {
                    ProductConfig requiredProduct =
                        _staticData.GetProduct(loadingZone.RequiredProductType);
                    ProductConfig heldProductConfig =
                        _staticData.GetProduct(heldProduct.ProductType);
                    player.SetInteractionPrompt(
                        $"Для заказа нужен товар: {requiredProduct.DisplayName} • " +
                        $"в руках: {heldProductConfig.DisplayName}",
                        false);
                    continue;
                }

                if (loadingZone.Slots.Length <= loadingZone.LoadedProductCount)
                {
                    player.SetInteractionPrompt(
                        "В машине клиента нет свободного места",
                        false);
                    continue;
                }

                ProductConfig product = _staticData.GetProduct(heldProduct.ProductType);
                player.SetInteractionPrompt(
                    $"E — загрузить клиенту • товар: {product.DisplayName}",
                    true);
            }
        }
    }
}
