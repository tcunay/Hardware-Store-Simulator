using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Common.Entity;

namespace HardwareStore.Gameplay.Features.Products.Systems
{
    public sealed class ApplyStockedProductPlacementSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _products;
        private readonly List<GameEntity> _buffer = new(32);

        public ApplyStockedProductPlacementSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _products = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.EntityId,
                    GameMatcher.InStock,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.StorageSlotIndex,
                    GameMatcher.ProductPlacementDirty,
                    GameMatcher.View,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody,
                    GameMatcher.Colliders,
                    GameMatcher.RigidbodyInterpolationMode,
                    GameMatcher.RigidbodyCollisionDetectionMode)
                .NoneOf(
                    GameMatcher.InboundProduct,
                    GameMatcher.Carried,
                    GameMatcher.LooseProduct,
                    GameMatcher.Loaded,
                    GameMatcher.DeliveryEntityId,
                    GameMatcher.LoadingZoneEntityId,
                    GameMatcher.DeliverySlotIndex,
                    GameMatcher.LoadingSlotIndex));
        }

        public void Execute()
        {
            foreach (GameEntity product in _products.GetEntities(_buffer))
            {
                GameEntity storageZone = _gameContext.GetRequiredEntity(
                    product.StorageZoneEntityId,
                    "stocked product storage zone");
                if (!storageZone.isStorageZone || !storageZone.hasSlots)
                    throw new InvalidOperationException(
                        $"Stocked product {product.EntityId} references an invalid storage zone.");
                if (product.StorageSlotIndex < 0 || product.StorageSlotIndex >= storageZone.Slots.Length)
                    throw new InvalidOperationException(
                        $"Stocked product {product.EntityId} references storage slot " +
                        $"{product.StorageSlotIndex}, but storage zone {storageZone.EntityId} has " +
                        $"{storageZone.Slots.Length} slots.");

                ProductPhysicsUtility.ConfigureInteractiveSlot(
                    product,
                    storageZone.Slots[product.StorageSlotIndex]);
                product.isProductPlacementDirty = false;
            }
        }

    }
}
