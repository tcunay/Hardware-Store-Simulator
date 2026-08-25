using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common;

namespace HardwareStore.Gameplay.Features.Products.Systems
{
    public sealed class ApplyTrolleyProductPlacementSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _products;
        private readonly List<GameEntity> _buffer = new(8);

        public ApplyTrolleyProductPlacementSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _products = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.EntityId,
                    GameMatcher.TrolleyEntityId,
                    GameMatcher.TrolleySlotIndex,
                    GameMatcher.ProductPlacementDirty,
                    GameMatcher.Interactable,
                    GameMatcher.View,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody,
                    GameMatcher.Colliders,
                    GameMatcher.RigidbodyInterpolationMode,
                    GameMatcher.RigidbodyCollisionDetectionMode)
                .AnyOf(
                    GameMatcher.ReservedDeliverySlotIndex,
                    GameMatcher.ReservedStorageSlotIndex)
                .NoneOf(
                    GameMatcher.CarrierEntityId,
                    GameMatcher.LooseProduct,
                    GameMatcher.Loaded,
                    GameMatcher.OrderLineEntityId,
                    GameMatcher.DeliverySlotIndex,
                    GameMatcher.StorageSlotIndex,
                    GameMatcher.LoadingSlotIndex,
                    GameMatcher.WorldPosition,
                    GameMatcher.WorldRotation));
        }

        public void Execute()
        {
            foreach (GameEntity product in _products.GetEntities(_buffer))
            {
                GameEntity trolley =
                    _gameContext.GetEntityWithEntityId(product.TrolleyEntityId);
                if (trolley == null || !trolley.isPlatformTrolley ||
                    trolley.isDestructed || !trolley.hasTrolleyCapacity ||
                    !trolley.hasSlots ||
                    trolley.Slots.Length != trolley.TrolleyCapacity ||
                    product.TrolleySlotIndex < 0 ||
                    product.TrolleySlotIndex >= trolley.TrolleyCapacity)
                {
                    throw new InvalidOperationException(
                        $"Product {product.EntityId} references invalid trolley placement.");
                }

                ValidateReservationState(product);

                ProductPhysicsUtility.ConfigureInteractiveSlot(
                    product,
                    trolley.Slots[product.TrolleySlotIndex]);
                product.isProductPlacementDirty = false;
            }
        }

        private void ValidateReservationState(GameEntity product)
        {
            if (product.isInboundProduct)
                InboundProductManifestValidator.Validate(_gameContext, product);

            bool validInbound = product.isInboundProduct &&
                                !product.isInStock &&
                                product.hasDeliveryEntityId &&
                                product.hasPurchaseOrderLineEntityId &&
                                product.hasReservedDeliverySlotIndex &&
                                !product.hasStorageZoneEntityId &&
                                !product.hasReservedStorageSlotIndex &&
                                !product.hasReservedOrderLineEntityId;
            bool validStock = !product.isInboundProduct &&
                              product.isInStock &&
                              !product.hasDeliveryEntityId &&
                              !product.hasReservedDeliverySlotIndex &&
                              product.hasStorageZoneEntityId &&
                              product.hasReservedStorageSlotIndex &&
                              product.hasReservedOrderLineEntityId;
            if (validInbound == validStock)
            {
                throw new InvalidOperationException(
                    $"Trolley product {product.EntityId} has invalid recovery reservations.");
            }
        }
    }
}
