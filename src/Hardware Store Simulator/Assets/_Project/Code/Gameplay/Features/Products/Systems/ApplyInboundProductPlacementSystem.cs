using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common;

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
                    GameMatcher.ProductType,
                    GameMatcher.InboundProduct,
                    GameMatcher.DeliveryEntityId,
                    GameMatcher.PurchaseOrderLineEntityId,
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
                    GameMatcher.CarrierEntityId,
                    GameMatcher.LooseProduct,
                    GameMatcher.Loaded,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.OrderLineEntityId,
                    GameMatcher.ReservedOrderLineEntityId,
                    GameMatcher.StorageSlotIndex,
                    GameMatcher.ReservedDeliverySlotIndex,
                    GameMatcher.ReservedStorageSlotIndex,
                    GameMatcher.LoadingSlotIndex,
                    GameMatcher.TrolleyEntityId,
                    GameMatcher.TrolleySlotIndex,
                    GameMatcher.WorldPosition,
                    GameMatcher.WorldRotation));
        }

        public void Execute()
        {
            foreach (GameEntity product in _products.GetEntities(_buffer))
            {
                InboundProductManifestValidator.Validate(_gameContext, product);
                GameEntity delivery = _gameContext.GetEntityWithEntityId(product.DeliveryEntityId);
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
