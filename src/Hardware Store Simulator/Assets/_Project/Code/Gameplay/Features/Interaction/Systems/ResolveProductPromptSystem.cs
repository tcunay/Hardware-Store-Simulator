using System;
using Entitas;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveProductPromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;

        public ResolveProductPromptSystem(GameContext gameContext)
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
                if (player.FocusedInteractionType != InteractionTypeId.Product)
                    continue;

                GameEntity product =
                    _gameContext.GetEntityWithEntityId(player.FocusedEntityId);
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
                    player.SetInteractionPrompt("Товар уже загружен клиенту", false);
                    continue;
                }

                if (player.isHandsOccupied)
                {
                    player.SetInteractionPrompt(
                        "Руки заняты — G, чтобы бросить мешок",
                        false);
                    continue;
                }

                if (product.isInboundProduct)
                {
                    player.SetInteractionPrompt(
                        "E — взять мешок из поставки",
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
                        "Клиент подъезжает — дождитесь его остановки",
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

                bool available =
                    product.ProductType == customerVisit.RequiredProductType;
                player.SetInteractionPrompt(
                    available
                        ? "E — взять мешок со склада"
                        : "Для активного заказа нужен другой товар",
                    available);
            }
        }
    }
}
