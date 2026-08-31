using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Products.Systems
{
    public sealed class SyncSlottedProductPoseSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _inboundProducts;
        private readonly IGroup<GameEntity> _loadedProducts;
        private readonly IGroup<GameEntity> _trolleyProducts;
        private readonly IGroup<GameEntity> _workerTrolleyProducts;
        private readonly List<GameEntity> _inboundBuffer = new(32);
        private readonly List<GameEntity> _loadedBuffer = new(32);
        private readonly List<GameEntity> _trolleyBuffer = new(8);
        private readonly List<GameEntity> _workerTrolleyBuffer = new(3);

        public SyncSlottedProductPoseSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _inboundProducts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.EntityId,
                    GameMatcher.InboundProduct,
                    GameMatcher.DeliveryEntityId,
                    GameMatcher.DeliverySlotIndex,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody)
                .NoneOf(GameMatcher.ProductPlacementDirty, GameMatcher.Destructed));
            _loadedProducts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.EntityId,
                    GameMatcher.Loaded,
                    GameMatcher.OrderLineEntityId,
                    GameMatcher.LoadingSlotIndex,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody)
                .NoneOf(GameMatcher.ProductPlacementDirty, GameMatcher.Destructed));
            _trolleyProducts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.EntityId,
                    GameMatcher.TrolleyEntityId,
                    GameMatcher.TrolleySlotIndex,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody)
                .NoneOf(GameMatcher.ProductPlacementDirty, GameMatcher.Destructed));
            _workerTrolleyProducts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.EntityId,
                    GameMatcher.WorkerTrolleyEntityId,
                    GameMatcher.WorkerTrolleySlotIndex,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody)
                .NoneOf(GameMatcher.ProductPlacementDirty, GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity product in _inboundProducts.GetEntities(_inboundBuffer))
                SyncInboundProduct(product);
            foreach (GameEntity product in _loadedProducts.GetEntities(_loadedBuffer))
                SyncLoadedProduct(product);
            foreach (GameEntity product in _trolleyProducts.GetEntities(_trolleyBuffer))
                SyncPlayerTrolleyProduct(product);
            foreach (GameEntity product in
                     _workerTrolleyProducts.GetEntities(_workerTrolleyBuffer))
                SyncWorkerTrolleyProduct(product);
        }

        private void SyncInboundProduct(GameEntity product)
        {
            GameEntity delivery = _gameContext.GetEntityWithEntityId(
                product.DeliveryEntityId);
            if (delivery == null || delivery.isDestructed || !delivery.isDelivery ||
                !delivery.hasSlots || product.DeliverySlotIndex < 0 ||
                product.DeliverySlotIndex >= delivery.Slots.Length)
            {
                throw new InvalidOperationException(
                    $"Inbound product {product.EntityId} references an invalid delivery slot.");
            }

            ProductPhysicsUtility.SyncSlotPose(
                product, delivery.Slots[product.DeliverySlotIndex]);
        }

        private void SyncLoadedProduct(GameEntity product)
        {
            GameEntity orderLine = _gameContext.GetEntityWithEntityId(
                product.OrderLineEntityId);
            if (orderLine == null || orderLine.isDestructed || !orderLine.isOrderLine ||
                !orderLine.hasOrderEntityId)
            {
                throw new InvalidOperationException(
                    $"Loaded product {product.EntityId} references an invalid order line.");
            }

            GameEntity visit = _gameContext.GetEntityWithEntityId(orderLine.OrderEntityId);
            if (visit == null || visit.isDestructed || !visit.isCustomerVisit ||
                !visit.hasSlots || product.LoadingSlotIndex < 0 ||
                product.LoadingSlotIndex >= visit.Slots.Length)
            {
                throw new InvalidOperationException(
                    $"Loaded product {product.EntityId} references an invalid vehicle slot.");
            }

            ProductPhysicsUtility.SyncSlotPose(
                product, visit.Slots[product.LoadingSlotIndex]);
        }

        private void SyncPlayerTrolleyProduct(GameEntity product)
        {
            GameEntity trolley = _gameContext.GetEntityWithEntityId(product.TrolleyEntityId);
            if (trolley == null || trolley.isDestructed || !trolley.isPlatformTrolley ||
                !trolley.hasSlots || product.TrolleySlotIndex < 0 ||
                product.TrolleySlotIndex >= trolley.Slots.Length)
            {
                throw new InvalidOperationException(
                    $"Product {product.EntityId} references an invalid trolley slot.");
            }

            ProductPhysicsUtility.SyncSlotPose(
                product, trolley.Slots[product.TrolleySlotIndex]);
        }

        private void SyncWorkerTrolleyProduct(GameEntity product)
        {
            GameEntity trolley = _gameContext.GetEntityWithEntityId(
                product.WorkerTrolleyEntityId);
            if (trolley == null || trolley.isDestructed || !trolley.isWorkerTrolley ||
                !trolley.isPlatformTrolley || !trolley.hasSlots ||
                product.WorkerTrolleySlotIndex < 0 ||
                product.WorkerTrolleySlotIndex >= trolley.Slots.Length)
            {
                throw new InvalidOperationException(
                    $"Product {product.EntityId} references an invalid worker-trolley slot.");
            }

            ProductPhysicsUtility.SyncSlotPose(
                product, trolley.Slots[product.WorkerTrolleySlotIndex]);
        }
    }
}
