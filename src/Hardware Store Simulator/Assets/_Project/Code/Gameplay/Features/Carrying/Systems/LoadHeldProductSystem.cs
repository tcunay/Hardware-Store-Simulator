using System;
using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Carrying.Systems
{
    public sealed class LoadHeldProductSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public LoadHeldProductSystem(GameContext gameContext, IGameEventFactory events)
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
                GameEntity loadingZone = _gameContext.GetRequiredEntity(
                    request.TargetEntityId,
                    "interaction target");
                if (!loadingZone.isLoadingZone)
                    continue;

                GameEntity player = _gameContext.GetRequiredEntity(
                    request.SourceEntityId,
                    "interaction source");
                if (!player.isPlayer)
                    throw new InvalidOperationException($"Interaction source {request.SourceEntityId} is not a player.");
                if (!player.hasStoreEntityId)
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} has no store relation.");
                if (!player.hasHeldProductId)
                    continue;

                if (!loadingZone.hasOrderEntityId)
                    throw new InvalidOperationException(
                        $"Loading zone {loadingZone.EntityId} has no order relation.");

                GameEntity order = _gameContext.GetRequiredEntity(
                    loadingZone.OrderEntityId,
                    "loading zone order");
                if (!order.isOrder || !order.hasStoreEntityId || !order.hasStorageZoneEntityId)
                    throw new InvalidOperationException(
                        $"Loading zone {loadingZone.EntityId} has an invalid order relation.");
                if (order.StoreEntityId != player.StoreEntityId)
                    continue;
                if (!order.isOrderActive || order.LoadedProductCount >= order.RequiredProductCount)
                    continue;

                int productId = player.HeldProductId;
                GameEntity product = _gameContext.GetRequiredEntity(productId, "player held product");
                if (!product.isProduct || !product.isCarried || !product.isInStock || product.isLoaded ||
                    product.ProductType != order.RequiredProductType)
                    continue;
                if (!product.hasStorageZoneEntityId)
                    throw new InvalidOperationException(
                        $"In-stock product {product.EntityId} has no storage ownership relation.");
                if (product.StorageZoneEntityId != order.StorageZoneEntityId)
                    throw new InvalidOperationException(
                        $"Held product {product.EntityId} belongs to storage " +
                        $"{product.StorageZoneEntityId}, not player storage " +
                        $"{order.StorageZoneEntityId}.");
                if (product.hasStorageSlotIndex)
                    throw new InvalidOperationException(
                        $"Carried product {product.EntityId} still occupies storage slot " +
                        $"{product.StorageSlotIndex}.");
                if (product.isLooseProduct || product.hasWorldPosition || product.hasWorldRotation)
                    throw new InvalidOperationException(
                        $"Carried product {product.EntityId} still contains loose placement state.");
                if (product.hasLoadingZoneEntityId || product.hasLoadingSlotIndex)
                    throw new InvalidOperationException(
                        $"Carried product {product.EntityId} already contains loading placement state.");
                if (!loadingZone.hasSlots || loadingZone.Slots.Length <= order.LoadedProductCount)
                    throw new InvalidOperationException(
                        $"Loading zone {loadingZone.EntityId} has insufficient slots for the order.");

                player.RemoveHeldProductId();
                product.isCarried = false;
                product.isInStock = false;
                product.isLoaded = true;
                product.isInteractable = false;
                product.RemoveStorageZoneEntityId();
                product.AddLoadingZoneEntityId(loadingZone.EntityId);
                product.AddLoadingSlotIndex(order.LoadedProductCount);
                product.isProductPlacementDirty = true;
                _events.EmitProductLoaded(productId, order.EntityId);
            }
        }
    }
}
