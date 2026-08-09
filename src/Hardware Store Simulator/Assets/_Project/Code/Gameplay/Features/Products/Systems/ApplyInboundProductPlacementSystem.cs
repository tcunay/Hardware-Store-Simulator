using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Common.Entity;

namespace HardwareStore.Gameplay.Features.Products.Systems
{
    public sealed class ApplyInboundProductPlacementSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _products;
        private readonly List<GameEntity> _buffer = new(32);

        public ApplyInboundProductPlacementSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _products = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.EntityId,
                    GameMatcher.InboundProduct,
                    GameMatcher.DeliveryEntityId,
                    GameMatcher.DeliverySlotIndex,
                    GameMatcher.ProductPlacementDirty,
                    GameMatcher.View,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody,
                    GameMatcher.Colliders,
                    GameMatcher.RigidbodyInterpolationMode,
                    GameMatcher.RigidbodyCollisionDetectionMode)
                .NoneOf(
                    GameMatcher.InStock,
                    GameMatcher.Carried,
                    GameMatcher.LooseProduct,
                    GameMatcher.Loaded,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.LoadingZoneEntityId,
                    GameMatcher.StorageSlotIndex,
                    GameMatcher.LoadingSlotIndex));
        }

        public void Execute()
        {
            foreach (GameEntity product in _products.GetEntities(_buffer))
            {
                GameEntity delivery = _gameContext.GetRequiredEntity(
                    product.DeliveryEntityId,
                    "inbound product delivery");
                if (!delivery.isDelivery || !delivery.isDeliveryActive || !delivery.hasSlots)
                    throw new InvalidOperationException(
                        $"Inbound product {product.EntityId} references an invalid delivery.");
                if (product.DeliverySlotIndex < 0 || product.DeliverySlotIndex >= delivery.Slots.Length)
                    throw new InvalidOperationException(
                        $"Inbound product {product.EntityId} references delivery slot " +
                        $"{product.DeliverySlotIndex}, but delivery {delivery.EntityId} has " +
                        $"{delivery.Slots.Length} slots.");

                ProductPhysicsUtility.ConfigureInteractiveSlot(
                    product,
                    delivery.Slots[product.DeliverySlotIndex]);
                product.isProductPlacementDirty = false;
            }
        }

    }
}
