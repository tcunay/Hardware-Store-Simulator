using System;
using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Carrying.Systems
{
    public sealed class PickUpProductSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public PickUpProductSystem(GameContext gameContext, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _requests = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.InteractionRequest,
                GameMatcher.SourceEntityId,
                GameMatcher.TargetEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity request in _requests)
            {
                GameEntity product = _gameContext.GetRequiredEntity(
                    request.TargetEntityId,
                    "interaction target");
                if (!product.isProduct)
                    continue;

                GameEntity player = _gameContext.GetRequiredEntity(
                    request.SourceEntityId,
                    "interaction source");
                if (!player.isPlayer)
                    throw new InvalidOperationException($"Interaction source {request.SourceEntityId} is not a player.");
                if (player.hasHeldProductId)
                    continue;
                if (product.isCarried || product.isLoaded)
                    continue;

                bool canPickUp = product.isInboundProduct
                    ? CanPickUpInbound(product, player)
                    : CanPickUpStock(product, player);
                if (!canPickUp)
                    continue;

                if (product.isInStock)
                    ReleaseStorageSlot(product);

                ReleaseDeliverySlot(product);
                ReleaseLoosePose(product);
                if (product.hasDeliverySlotIndex || product.hasStorageSlotIndex ||
                    product.hasLoadingZoneEntityId || product.hasLoadingSlotIndex)
                {
                    throw new InvalidOperationException(
                        $"Product {product.EntityId} contains stale slot placement state.");
                }

                int productId = product.EntityId;
                player.AddHeldProductId(productId);
                product.isCarried = true;
                product.isInteractable = false;
                product.isProductPlacementDirty = true;
                _events.EmitAudio(AudioCueId.PickUp);
            }
        }

        private bool CanPickUpInbound(GameEntity product, GameEntity player)
        {
            if (!product.hasDeliveryEntityId)
                throw new InvalidOperationException(
                    $"Inbound product {product.EntityId} has no delivery relation.");

            GameEntity delivery = _gameContext.GetRequiredEntity(
                product.DeliveryEntityId,
                "inbound product delivery");
            if (!delivery.isDelivery || !delivery.isDeliveryActive || !delivery.hasStoreEntityId)
                throw new InvalidOperationException(
                    $"Inbound product {product.EntityId} is linked to an invalid delivery.");

            GameEntity store = GetPlayerStore(player);
            return delivery.StoreEntityId == store.EntityId;
        }

        private GameEntity GetPlayerStore(GameEntity player)
        {
            if (!player.hasStoreEntityId)
                throw new InvalidOperationException(
                    $"Player {player.EntityId} has no store relation.");

            GameEntity store = _gameContext.GetRequiredEntity(player.StoreEntityId, "player store");
            if (!store.isStore)
                throw new InvalidOperationException($"Entity {player.StoreEntityId} is not a store.");

            return store;
        }

        private GameEntity GetStoreOrder(GameEntity store)
        {
            if (!store.hasOrderEntityId)
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has no order relation.");

            GameEntity order = _gameContext.GetRequiredEntity(store.OrderEntityId, "store order");
            if (!order.isOrder)
                throw new InvalidOperationException($"Entity {store.OrderEntityId} is not an order.");

            return order;
        }

        private bool CanPickUpStock(GameEntity product, GameEntity player)
        {
            if (!product.isInStock)
                return false;
            if (!product.hasStorageZoneEntityId)
                throw new InvalidOperationException(
                    $"In-stock product {product.EntityId} has no storage ownership relation.");

            GameEntity store = GetPlayerStore(player);
            if (!store.hasStorageZoneEntityId)
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has no storage relation.");
            if (product.StorageZoneEntityId != store.StorageZoneEntityId)
                return false;

            GameEntity order = GetStoreOrder(store);
            return order.isOrderActive && product.ProductType == order.RequiredProductType;
        }

        private static void ReleaseDeliverySlot(GameEntity product)
        {
            if (product.hasDeliverySlotIndex)
                product.RemoveDeliverySlotIndex();
        }

        private static void ReleaseStorageSlot(GameEntity product)
        {
            if (!product.hasStorageZoneEntityId)
                throw new InvalidOperationException(
                    $"In-stock product {product.EntityId} has no storage ownership relation.");

            if (product.hasStorageSlotIndex)
                product.RemoveStorageSlotIndex();
        }

        private static void ReleaseLoosePose(GameEntity product)
        {
            if (!product.isLooseProduct)
            {
                if (product.hasWorldPosition || product.hasWorldRotation)
                    throw new InvalidOperationException(
                        $"Non-loose product {product.EntityId} contains a loose world pose.");

                return;
            }

            if (!product.hasWorldPosition || !product.hasWorldRotation)
                throw new InvalidOperationException(
                    $"Loose product {product.EntityId} has an incomplete world pose.");

            product.isLooseProduct = false;
            product.RemoveWorldPosition();
            product.RemoveWorldRotation();
        }
    }
}
