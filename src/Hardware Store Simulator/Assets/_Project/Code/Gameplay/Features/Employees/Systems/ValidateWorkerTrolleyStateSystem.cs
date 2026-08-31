using System;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class ValidateWorkerTrolleyStateSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly PlatformTrolleyConfig _config;
        private readonly IGroup<GameEntity> _trolleys;
        private readonly bool[] _occupiedSlots;

        public ValidateWorkerTrolleyStateSystem(GameContext gameContext,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _config = staticData.PlatformTrolley;
            _occupiedSlots = new bool[_config.Capacity];
            _trolleys = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WorkerTrolley, GameMatcher.PlatformTrolley,
                    GameMatcher.EntityId, GameMatcher.TrolleyStoreEntityId,
                    GameMatcher.WorkerTrolleyStoreEntityId,
                    GameMatcher.TrolleyCapacity,
                    GameMatcher.OccupiedTrolleySlotCount,
                    GameMatcher.TrolleyMovementSpeed,
                    GameMatcher.TrolleyFollowDistance,
                    GameMatcher.WorkerTrolleyHomePosition,
                    GameMatcher.WorkerTrolleyHomeRotation,
                    GameMatcher.WorkerTrolleyCustomerLoadingPosition,
                    GameMatcher.WorkerTrolleyCustomerLoadingRotation,
                    GameMatcher.Transform, GameMatcher.Rigidbody,
                    GameMatcher.Colliders, GameMatcher.Slots)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity trolley in _trolleys)
            {
                ValidateConfiguration(trolley);
                int cargoCount = ValidateCargo(trolley);
                if (cargoCount != trolley.OccupiedTrolleySlotCount)
                    throw new InvalidOperationException(
                        $"Worker trolley {trolley.EntityId} reports invalid occupancy.");
                ValidatePusher(trolley, cargoCount);
            }
        }

        private void ValidateConfiguration(GameEntity trolley)
        {
            GameEntity store = _gameContext.GetEntityWithEntityId(
                trolley.WorkerTrolleyStoreEntityId);
            if (store == null || store.isDestructed || !store.isStore ||
                trolley.TrolleyStoreEntityId !=
                trolley.WorkerTrolleyStoreEntityId ||
                trolley.isInteractable ||
                trolley.TrolleyCapacity != _config.Capacity ||
                trolley.TrolleyCapacity != _occupiedSlots.Length ||
                trolley.Slots.Length != trolley.TrolleyCapacity ||
                trolley.OccupiedTrolleySlotCount < 0 ||
                trolley.OccupiedTrolleySlotCount > trolley.TrolleyCapacity ||
                trolley.TrolleyMovementSpeed != _config.MovementSpeed ||
                trolley.TrolleyFollowDistance != _config.FollowDistance ||
                _gameContext.GetEntitiesWithTrolleyEntityId(
                    trolley.EntityId).Count != 0 ||
                !trolley.Rigidbody.detectCollisions)
                throw new InvalidOperationException(
                    $"Worker trolley {trolley.EntityId} has invalid configuration.");
            bool parked = trolley.Rigidbody.isKinematic &&
                          !trolley.Rigidbody.useGravity;
            bool hitched = !trolley.Rigidbody.isKinematic &&
                           trolley.Rigidbody.useGravity &&
                           trolley.Rigidbody.interpolation ==
                           RigidbodyInterpolation.Interpolate &&
                           trolley.Rigidbody.collisionDetectionMode ==
                           CollisionDetectionMode.ContinuousDynamic &&
                           (trolley.Rigidbody.constraints &
                            RigidbodyConstraints.FreezeRotationX) != 0 &&
                           (trolley.Rigidbody.constraints &
                            RigidbodyConstraints.FreezeRotationZ) != 0;
            if (!trolley.hasTrolleyPusherEntityId && !parked ||
                trolley.hasTrolleyPusherEntityId && !parked && !hitched)
            {
                throw new InvalidOperationException(
                    $"Worker trolley {trolley.EntityId} has an invalid parked/hitched " +
                    "physics state.");
            }
            foreach (UnityEngine.Transform slot in trolley.Slots)
            {
                if (slot == null)
                    throw new InvalidOperationException(
                        $"Worker trolley {trolley.EntityId} has a missing slot.");
            }
        }

        private int ValidateCargo(GameEntity trolley)
        {
            Array.Clear(_occupiedSlots, 0, _occupiedSlots.Length);
            int count = 0;
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithWorkerTrolleyEntityId(
                         trolley.EntityId))
            {
                if (product.isDestructed || !product.isProduct ||
                    product.isLoaded || product.isInteractable ||
                    !product.hasEntityId || !product.hasWorkerTrolleySlotIndex ||
                    product.WorkerTrolleySlotIndex < 0 ||
                    product.WorkerTrolleySlotIndex >= _occupiedSlots.Length ||
                    _occupiedSlots[product.WorkerTrolleySlotIndex] ||
                    !product.hasWarehouseRunEntityId ||
                    product.hasStorageSlotIndex || product.hasCarrierEntityId ||
                    product.hasOrderLineEntityId || product.hasLoadingSlotIndex ||
                    product.hasTrolleyEntityId || product.hasTrolleySlotIndex)
                    throw new InvalidOperationException(
                        $"Worker trolley {trolley.EntityId} has invalid cargo.");
                GameEntity run = _gameContext.GetEntityWithEntityId(
                    product.WarehouseRunEntityId);
                if (run == null || run.isDestructed ||
                    !run.hasWarehouseTaskWorkerTrolleyEntityId ||
                    run.WarehouseTaskWorkerTrolleyEntityId != trolley.EntityId ||
                    !IsValidRunCargo(run, product))
                    throw new InvalidOperationException(
                        $"Worker trolley product {product.EntityId} has invalid run.");
                _occupiedSlots[product.WorkerTrolleySlotIndex] = true;
                count++;
            }
            return count;
        }

        private void ValidatePusher(GameEntity trolley, int cargoCount)
        {
            if (!trolley.hasTrolleyPusherEntityId)
            {
                if (cargoCount != 0)
                    throw new InvalidOperationException(
                        $"Unpushed worker trolley {trolley.EntityId} contains cargo.");
                return;
            }
            GameEntity worker = _gameContext.GetEntityWithEntityId(
                trolley.TrolleyPusherEntityId);
            if (worker == null || worker.isDestructed ||
                !worker.isWarehouseWorker || !worker.isHandsOccupied ||
                !worker.isPushingWorkerTrolley || worker.isCarryingProduct ||
                !worker.hasWarehouseWorkerStoreEntityId ||
                !worker.hasRigidbody || !worker.hasColliders ||
                worker.WarehouseWorkerStoreEntityId !=
                trolley.WorkerTrolleyStoreEntityId ||
                !worker.hasWarehouseWorkerStatus ||
                worker.WarehouseWorkerStatus is not
                    (WarehouseWorkerStatusId.MovingToWorkerTrolley or
                     WarehouseWorkerStatusId.MovingWorkerTrolleyToPickup or
                     WarehouseWorkerStatusId.MovingWorkerTrolleyToStorage or
                     WarehouseWorkerStatusId.MovingWorkerTrolleyToCustomerLoading or
                     WarehouseWorkerStatusId.ReturningWorkerTrolley))
                throw new InvalidOperationException(
                    $"Worker trolley {trolley.EntityId} has an invalid pusher.");

            if (!trolley.Rigidbody.isKinematic)
            {
                bool hasPhysicalHitch = false;
                foreach (ConfigurableJoint joint in
                         trolley.Rigidbody.GetComponents<ConfigurableJoint>())
                {
                    if (joint != null && joint.connectedBody == worker.Rigidbody)
                    {
                        hasPhysicalHitch = true;
                        break;
                    }
                }
                if (!hasPhysicalHitch)
                {
                    throw new InvalidOperationException(
                        $"Worker trolley {trolley.EntityId} has an invalid physical hitch.");
                }
            }
        }

        private bool IsValidRunCargo(GameEntity run, GameEntity product)
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
