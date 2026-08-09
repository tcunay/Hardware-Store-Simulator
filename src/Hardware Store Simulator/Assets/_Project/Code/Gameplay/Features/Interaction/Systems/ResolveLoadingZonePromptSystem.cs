using System;
using Entitas;
using HardwareStore.Common.Entity;
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

                GameEntity loadingZone = _gameContext.GetRequiredEntity(
                    player.FocusedEntityId,
                    "focused customer loading zone");
                if (!loadingZone.isLoadingZone || !loadingZone.hasOrderEntityId)
                    throw new InvalidOperationException(
                        $"Entity {loadingZone.EntityId} is not a configured loading zone.");

                GameEntity store = _gameContext.RequireStore(player);
                if (loadingZone.OrderEntityId != store.OrderEntityId)
                    continue;

                GameEntity order = _gameContext.GetRequiredEntity(
                    store.OrderEntityId,
                    "store order");
                if (!order.isOrder ||
                    order.StoreEntityId != store.EntityId ||
                    order.StorageZoneEntityId != store.StorageZoneEntityId)
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} has an invalid order relation.");

                if (order.isOrderWaiting)
                {
                    player.SetInteractionPrompt(
                        "Сначала примите заказ у стойки",
                        false);
                    continue;
                }

                if (order.isOrderCompleted)
                {
                    player.SetInteractionPrompt(
                        "Машина загружена — заказ выполнен",
                        false);
                    continue;
                }

                if (!order.isOrderActive)
                    throw new InvalidOperationException(
                        $"Order {order.EntityId} has no valid lifecycle state.");

                if (!player.hasHeldProductId)
                {
                    player.SetInteractionPrompt(
                        "Принесите сюда товар со склада",
                        false);
                    continue;
                }

                GameEntity heldProduct = _gameContext.GetRequiredEntity(
                    player.HeldProductId,
                    "player held product");
                if (!heldProduct.isProduct || !heldProduct.isCarried)
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} holds invalid product {heldProduct.EntityId}.");
                if (heldProduct.isInStock && !heldProduct.hasStorageZoneEntityId)
                    throw new InvalidOperationException(
                        $"In-stock product {heldProduct.EntityId} has no storage ownership relation.");
                if (heldProduct.isCarried && heldProduct.hasStorageSlotIndex)
                    throw new InvalidOperationException(
                        $"Carried product {heldProduct.EntityId} still occupies storage slot " +
                        $"{heldProduct.StorageSlotIndex}.");

                bool available = heldProduct.isInStock &&
                                 heldProduct.StorageZoneEntityId == store.StorageZoneEntityId &&
                                 heldProduct.ProductType == order.RequiredProductType;
                if (!available)
                {
                    player.SetInteractionPrompt(
                        "Для заказа нужен принятый на склад цемент",
                        false);
                    continue;
                }

                if (!loadingZone.hasSlots ||
                    loadingZone.Slots.Length <= order.LoadedProductCount)
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
