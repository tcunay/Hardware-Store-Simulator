using System;
using System.Collections.Generic;
using Entitas;

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
                    GameMatcher.CustomerVisitEntityId,
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
                    GameMatcher.CarrierEntityId,
                    GameMatcher.LooseProduct,
                    GameMatcher.DeliveryEntityId,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.DeliverySlotIndex,
                    GameMatcher.StorageSlotIndex,
                    GameMatcher.WorldPosition,
                    GameMatcher.WorldRotation));
        }

        public void Execute()
        {
            foreach (GameEntity product in _products.GetEntities(_buffer))
            {
                GameEntity customerVisit =
                    _gameContext.GetEntityWithEntityId(product.CustomerVisitEntityId);
                if (product.LoadingSlotIndex < 0 ||
                    product.LoadingSlotIndex >= customerVisit.Slots.Length)
                    throw new InvalidOperationException(
                        $"Loaded product {product.EntityId} references loading slot " +
                        $"{product.LoadingSlotIndex}, but customer visit " +
                        $"{customerVisit.EntityId} has {customerVisit.Slots.Length} slots.");

                ProductPhysicsUtility.ConfigureLockedSlot(
                    product,
                    customerVisit.Slots[product.LoadingSlotIndex]);
                product.isProductPlacementDirty = false;
            }
        }
    }
}
