using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Common.Entity;

namespace HardwareStore.Gameplay.Features.Products.Systems
{
    public sealed class ApplyLoadedProductPlacementSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _products;
        private readonly List<GameEntity> _buffer = new(32);

        public ApplyLoadedProductPlacementSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _products = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.EntityId,
                    GameMatcher.Loaded,
                    GameMatcher.LoadingZoneEntityId,
                    GameMatcher.LoadingSlotIndex,
                    GameMatcher.ProductPlacementDirty,
                    GameMatcher.View,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody,
                    GameMatcher.Colliders,
                    GameMatcher.RigidbodyInterpolationMode,
                    GameMatcher.RigidbodyCollisionDetectionMode)
                .NoneOf(
                    GameMatcher.InboundProduct,
                    GameMatcher.InStock,
                    GameMatcher.Carried,
                    GameMatcher.LooseProduct,
                    GameMatcher.DeliveryEntityId,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.DeliverySlotIndex,
                    GameMatcher.StorageSlotIndex));
        }

        public void Execute()
        {
            foreach (GameEntity product in _products.GetEntities(_buffer))
            {
                GameEntity loadingZone = _gameContext.GetRequiredEntity(
                    product.LoadingZoneEntityId,
                    "loaded product loading zone");
                if (!loadingZone.isLoadingZone || !loadingZone.hasSlots)
                    throw new InvalidOperationException(
                        $"Loaded product {product.EntityId} references an invalid loading zone.");
                if (product.LoadingSlotIndex < 0 || product.LoadingSlotIndex >= loadingZone.Slots.Length)
                    throw new InvalidOperationException(
                        $"Loaded product {product.EntityId} references loading slot " +
                        $"{product.LoadingSlotIndex}, but loading zone {loadingZone.EntityId} has " +
                        $"{loadingZone.Slots.Length} slots.");

                ProductPhysicsUtility.ConfigureLockedSlot(
                    product,
                    loadingZone.Slots[product.LoadingSlotIndex]);
                product.isProductPlacementDirty = false;
            }
        }

    }
}
