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
                    GameMatcher.OrderLineEntityId,
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
                GameEntity orderLine =
                    _gameContext.GetEntityWithEntityId(product.OrderLineEntityId);
                if (!orderLine.isOrderLine || orderLine.isDestructed ||
                    !orderLine.hasOrderEntityId || !orderLine.hasEntityId ||
                    orderLine.EntityId != product.OrderLineEntityId)
                {
                    throw new InvalidOperationException(
                        $"Loaded product {product.EntityId} references invalid order line " +
                        $"{product.OrderLineEntityId}.");
                }

                GameEntity customerVisit =
                    _gameContext.GetEntityWithEntityId(orderLine.OrderEntityId);
                if (!customerVisit.isCustomerVisit || !customerVisit.hasEntityId ||
                    !customerVisit.hasSlots || customerVisit.EntityId != orderLine.OrderEntityId)
                {
                    throw new InvalidOperationException(
                        $"Order line {orderLine.EntityId} references invalid customer visit " +
                        $"{orderLine.OrderEntityId}.");
                }
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
