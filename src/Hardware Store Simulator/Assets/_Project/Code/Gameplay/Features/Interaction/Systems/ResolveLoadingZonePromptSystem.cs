using System;
using Entitas;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveLoadingZonePromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;

        public ResolveLoadingZonePromptSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
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
                    player.SetInteractionPrompt(
                        "Принесите сюда товар со склада",
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
                    player.SetInteractionPrompt(
                        "Для заказа нужен принятый на склад цемент",
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

                player.SetInteractionPrompt("E — загрузить мешок клиенту", true);
            }
        }
    }
}
