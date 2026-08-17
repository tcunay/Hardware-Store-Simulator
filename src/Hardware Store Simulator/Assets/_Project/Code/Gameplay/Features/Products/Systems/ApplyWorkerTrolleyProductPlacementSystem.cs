using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Products.Systems
{
    public sealed class ApplyWorkerTrolleyProductPlacementSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _products;
        private readonly List<GameEntity> _buffer = new(3);

        public ApplyWorkerTrolleyProductPlacementSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _products = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product, GameMatcher.EntityId,
                    GameMatcher.WorkerTrolleyEntityId,
                    GameMatcher.WorkerTrolleySlotIndex,
                    GameMatcher.WarehouseRunEntityId,
                    GameMatcher.ReservedStorageSlotIndex,
                    GameMatcher.ReservedOrderLineEntityId,
                    GameMatcher.ReservedCustomerLoadingSlotIndex,
                    GameMatcher.ProductPlacementDirty,
                    GameMatcher.View, GameMatcher.Transform,
                    GameMatcher.Rigidbody, GameMatcher.Colliders,
                    GameMatcher.RigidbodyInterpolationMode,
                    GameMatcher.RigidbodyCollisionDetectionMode)
                .NoneOf(
                    GameMatcher.InboundProduct, GameMatcher.CarrierEntityId,
                    GameMatcher.LooseProduct, GameMatcher.Loaded,
                    GameMatcher.OrderLineEntityId, GameMatcher.DeliverySlotIndex,
                    GameMatcher.StorageSlotIndex, GameMatcher.LoadingSlotIndex,
                    GameMatcher.TrolleyEntityId, GameMatcher.TrolleySlotIndex,
                    GameMatcher.WorldPosition, GameMatcher.WorldRotation));
        }

        public void Execute()
        {
            foreach (GameEntity product in _products.GetEntities(_buffer))
            {
                GameEntity trolley = _gameContext.GetEntityWithEntityId(
                    product.WorkerTrolleyEntityId);
                GameEntity run = _gameContext.GetEntityWithEntityId(
                    product.WarehouseRunEntityId);
                if (trolley == null || trolley.isDestructed ||
                    !trolley.isWorkerTrolley || trolley.isPlatformTrolley ||
                    !trolley.hasEntityId || !trolley.hasTrolleyCapacity ||
                    !trolley.hasSlots ||
                    trolley.Slots.Length != trolley.TrolleyCapacity ||
                    product.WorkerTrolleySlotIndex < 0 ||
                    product.WorkerTrolleySlotIndex >= trolley.TrolleyCapacity ||
                    run == null || run.isDestructed || !run.isWarehouseTask ||
                    !run.isWorkerTrolleyCustomerLoadingRun ||
                    !run.hasWarehouseTaskWorkerTrolleyEntityId ||
                    run.WarehouseTaskWorkerTrolleyEntityId != trolley.EntityId ||
                    product.isInteractable || !product.isInStock)
                    throw new InvalidOperationException(
                        $"Product {product.EntityId} has invalid worker-trolley placement.");

                ProductPhysicsUtility.ConfigureLockedSlot(
                    product, trolley.Slots[product.WorkerTrolleySlotIndex]);
                product.isProductPlacementDirty = false;
            }
        }
    }
}
