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
                    GameMatcher.ProductPlacementDirty,
                    GameMatcher.View, GameMatcher.Transform,
                    GameMatcher.Rigidbody, GameMatcher.Colliders,
                    GameMatcher.RigidbodyInterpolationMode,
                    GameMatcher.RigidbodyCollisionDetectionMode)
                .NoneOf(
                    GameMatcher.CarrierEntityId,
                    GameMatcher.LooseProduct, GameMatcher.Loaded,
                    GameMatcher.OrderLineEntityId,
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
                    !trolley.isWorkerTrolley || !trolley.isPlatformTrolley ||
                    !trolley.hasEntityId || !trolley.hasTrolleyCapacity ||
                    !trolley.hasSlots ||
                    trolley.Slots.Length != trolley.TrolleyCapacity ||
                    product.WorkerTrolleySlotIndex < 0 ||
                    product.WorkerTrolleySlotIndex >= trolley.TrolleyCapacity ||
                    run == null || run.isDestructed || !run.isWarehouseTask ||
                    !run.hasWarehouseTaskWorkerTrolleyEntityId ||
                    run.WarehouseTaskWorkerTrolleyEntityId != trolley.EntityId ||
                    product.isInteractable ||
                    !HasValidRunPlacement(run, product))
                    throw new InvalidOperationException(
                        $"Product {product.EntityId} has invalid worker-trolley placement.");

                ProductPhysicsUtility.ConfigureLockedSlot(
                    product, trolley.Slots[product.WorkerTrolleySlotIndex]);
                product.isProductPlacementDirty = false;
            }
        }

        private bool HasValidRunPlacement(GameEntity run, GameEntity product)
        {
            if (run.isWorkerTrolleyCustomerLoadingRun &&
                !run.isInboundToStorageTask &&
                !run.isWorkerTrolleyInboundStorageRun)
            {
                return product.isInStock && !product.isInboundProduct &&
                       product.hasStorageZoneEntityId &&
                       product.hasReservedStorageSlotIndex &&
                       product.hasReservedOrderLineEntityId &&
                       product.hasReservedCustomerLoadingSlotIndex &&
                       !product.hasDeliverySlotIndex &&
                       !product.hasReservedDeliverySlotIndex;
            }
            if (!run.isWorkerTrolleyInboundStorageRun ||
                !run.isInboundToStorageTask ||
                run.isWorkerTrolleyCustomerLoadingRun ||
                product.isInStock || !product.isInboundProduct ||
                !product.hasDeliveryEntityId ||
                !product.hasPurchaseOrderLineEntityId ||
                product.hasDeliverySlotIndex ||
                !product.hasReservedDeliverySlotIndex ||
                product.hasStorageZoneEntityId ||
                product.hasReservedStorageSlotIndex ||
                product.hasReservedOrderLineEntityId ||
                product.hasReservedCustomerLoadingSlotIndex)
            {
                return false;
            }

            GameEntity task =
                _gameContext.GetEntityWithWarehouseTaskProductEntityId(
                    product.EntityId);
            return task != null && !task.isDestructed &&
                   task.isWarehouseTask && task.isInboundToStorageTask &&
                   task.hasWarehouseTaskStorageZoneEntityId &&
                   task.hasWarehouseTaskReservedStorageSlotIndex &&
                   (task == run) == task.isWorkerTrolleyInboundStorageRun;
        }
    }
}
