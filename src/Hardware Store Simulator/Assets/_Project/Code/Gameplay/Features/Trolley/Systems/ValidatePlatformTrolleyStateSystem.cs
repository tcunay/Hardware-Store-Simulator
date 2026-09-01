using System;
using Entitas;
using HardwareStore.Gameplay.Common;
using HardwareStore.Gameplay.Common.Physics;
using HardwareStore.Gameplay.Components;
using UnityEngine;
using UnityEngine.AI;

namespace HardwareStore.Gameplay.Features.Trolley.Systems
{
    public sealed class ValidatePlatformTrolleyStateSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _trolleys;

        public ValidatePlatformTrolleyStateSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _trolleys = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.PlatformTrolley,
                    GameMatcher.EntityId,
                    GameMatcher.TrolleyStoreEntityId,
                    GameMatcher.TrolleyCapacity,
                    GameMatcher.OccupiedTrolleySlotCount,
                    GameMatcher.TrolleyMovementSpeed,
                    GameMatcher.TrolleyFollowDistance,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody,
                    GameMatcher.Colliders,
                    GameMatcher.NavMeshObstacle,
                    GameMatcher.Slots)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity trolley in _trolleys)
            {
                ValidateConfiguration(trolley);
                if (trolley.isWorkerTrolley)
                {
                    ValidateWorkerLease(trolley);
                    GhostMoverCollisionProfile.Validate(
                        trolley.Rigidbody,
                        trolley.Colliders,
                        GhostMoverCollisionProfile.GhostMover);
                    continue;
                }
                int cargoCount = ValidateCargo(trolley);
                if (cargoCount != trolley.OccupiedTrolleySlotCount)
                {
                    throw new InvalidOperationException(
                        $"Trolley {trolley.EntityId} reports " +
                        $"{trolley.OccupiedTrolleySlotCount} occupied slots, found " +
                        $"{cargoCount} cargo entities.");
                }

                ValidatePusher(trolley);
                GhostMoverCollisionProfile.Validate(
                    trolley.Rigidbody,
                    trolley.Colliders,
                    trolley.hasTrolleyPusherEntityId
                        ? GhostMoverCollisionProfile.TrafficObstacle
                        : GhostMoverCollisionProfile.GhostMover);
            }
        }

        private void ValidateConfiguration(GameEntity trolley)
        {
            GameEntity store =
                _gameContext.GetEntityWithEntityId(trolley.TrolleyStoreEntityId);
            bool parkedBody = trolley.Rigidbody.isKinematic &&
                              !trolley.Rigidbody.useGravity;
            NavMeshObstacle obstacle = trolley.NavMeshObstacle;
            bool validNavigationObstacle =
                obstacle.gameObject == trolley.Rigidbody.gameObject &&
                trolley.Transform == trolley.Rigidbody.transform &&
                obstacle.shape == NavMeshObstacleShape.Box &&
                obstacle.center == new Vector3(0f, 0.27f, 0.15f) &&
                obstacle.size == new Vector3(2f, 0.5f, 2.1f) &&
                obstacle.carving && obstacle.carveOnlyStationary &&
                Mathf.Approximately(obstacle.carvingMoveThreshold, 0.05f) &&
                Mathf.Approximately(obstacle.carvingTimeToStationary, 0.1f) &&
                obstacle.enabled == (!trolley.isWorkerTrolley &&
                                     !trolley.hasTrolleyPusherEntityId);
            if (store == null || !store.isStore || store.isDestructed ||
                trolley.TrolleyCapacity <= 0 ||
                trolley.Slots.Length != trolley.TrolleyCapacity ||
                trolley.OccupiedTrolleySlotCount < 0 ||
                trolley.OccupiedTrolleySlotCount > trolley.TrolleyCapacity ||
                trolley.TrolleyMovementSpeed <= 0f ||
                trolley.TrolleyFollowDistance <= 0f ||
                !IsFinite(trolley.TrolleyMovementSpeed) ||
                !IsFinite(trolley.TrolleyFollowDistance) ||
                !validNavigationObstacle ||
                !parkedBody)
            {
                throw new InvalidOperationException(
                    $"Platform trolley {trolley.EntityId} has invalid configuration.");
            }

            for (int index = 0; index < trolley.Slots.Length; index++)
            {
                if (trolley.Slots[index] == null)
                    throw new InvalidOperationException(
                        $"Platform trolley {trolley.EntityId} has a missing slot {index}.");
            }
        }

        private int ValidateCargo(GameEntity trolley)
        {
            var occupiedSlots = new bool[trolley.TrolleyCapacity];
            int cargoCount = 0;
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithTrolleyEntityId(trolley.EntityId))
            {
                if (product.isInboundProduct)
                    InboundProductManifestValidator.Validate(_gameContext, product);

                bool hasInboundReservation =
                    product.isInboundProduct && !product.isInStock &&
                    product.hasDeliveryEntityId &&
                    product.hasPurchaseOrderLineEntityId &&
                    product.hasReservedDeliverySlotIndex &&
                    !product.hasStorageZoneEntityId &&
                    !product.hasReservedStorageSlotIndex &&
                    !product.hasReservedOrderLineEntityId;
                bool hasStockReservation =
                    product.isInStock && !product.isInboundProduct &&
                    product.hasStorageZoneEntityId &&
                    product.hasReservedStorageSlotIndex &&
                    product.hasReservedOrderLineEntityId &&
                    !product.hasDeliveryEntityId &&
                    !product.hasReservedDeliverySlotIndex;
                if (product.isDestructed || !product.isProduct ||
                    !product.hasEntityId ||
                    !product.hasTrolleySlotIndex ||
                    (!hasInboundReservation && !hasStockReservation) ||
                    product.TrolleySlotIndex < 0 ||
                    product.TrolleySlotIndex >= occupiedSlots.Length ||
                    occupiedSlots[product.TrolleySlotIndex] ||
                    product.hasCarrierEntityId || product.hasDeliverySlotIndex ||
                    product.hasStorageSlotIndex || product.hasOrderLineEntityId ||
                    product.hasLoadingSlotIndex || product.isLooseProduct ||
                    product.isLoaded || product.hasWorldPosition ||
                    product.hasWorldRotation || !product.isInteractable)
                {
                    throw new InvalidOperationException(
                        $"Trolley {trolley.EntityId} contains invalid cargo state.");
                }

                occupiedSlots[product.TrolleySlotIndex] = true;
                cargoCount++;
            }

            return cargoCount;
        }

        private void ValidatePusher(GameEntity trolley)
        {
            if (!trolley.hasTrolleyPusherEntityId)
            {
                if (!trolley.isInteractable)
                    throw new InvalidOperationException(
                        $"Parked trolley {trolley.EntityId} is not interactable.");
                return;
            }

            GameEntity player =
                _gameContext.GetEntityWithEntityId(trolley.TrolleyPusherEntityId);
            if (player == null || !player.isPlayer || player.isDestructed ||
                !player.isHandsOccupied || !player.isPushingTrolley ||
                player.isCarryingProduct || trolley.isInteractable)
            {
                throw new InvalidOperationException(
                    $"Trolley {trolley.EntityId} has an invalid pusher relation.");
            }
        }

        private void ValidateWorkerLease(GameEntity trolley)
        {
            if (trolley.isInteractable ||
                !trolley.hasWorkerTrolleyStoreEntityId ||
                trolley.WorkerTrolleyStoreEntityId !=
                trolley.TrolleyStoreEntityId ||
                !trolley.hasWorkerTrolleyHomePosition ||
                !trolley.hasWorkerTrolleyHomeRotation ||
                !trolley.hasWorkerTrolleyCustomerLoadingPosition ||
                !trolley.hasWorkerTrolleyCustomerLoadingRotation ||
                _gameContext.GetEntitiesWithTrolleyEntityId(
                    trolley.EntityId).Count != 0)
            {
                throw new InvalidOperationException(
                    $"Platform trolley {trolley.EntityId} has an invalid worker lease.");
            }

            int cargoCount = _gameContext.GetEntitiesWithWorkerTrolleyEntityId(
                trolley.EntityId).Count;
            if (cargoCount != trolley.OccupiedTrolleySlotCount)
            {
                throw new InvalidOperationException(
                    $"Leased trolley {trolley.EntityId} reports invalid occupancy.");
            }

            if (!trolley.hasTrolleyPusherEntityId)
            {
                if (cargoCount != 0)
                    throw new InvalidOperationException(
                        $"Unpushed leased trolley {trolley.EntityId} contains cargo.");
                return;
            }

            GameEntity worker = _gameContext.GetEntityWithEntityId(
                trolley.TrolleyPusherEntityId);
            if (worker == null || worker.isDestructed ||
                !worker.isWarehouseWorker || !worker.hasEntityId ||
                !worker.isHandsOccupied || !worker.isPushingWorkerTrolley ||
                worker.isCarryingProduct ||
                !worker.hasWarehouseWorkerStoreEntityId ||
                worker.WarehouseWorkerStoreEntityId !=
                trolley.TrolleyStoreEntityId ||
                !worker.hasWarehouseWorkerStatus ||
                worker.WarehouseWorkerStatus is not
                    (WarehouseWorkerStatusId.MovingToWorkerTrolley or
                     WarehouseWorkerStatusId.MovingWorkerTrolleyToPickup or
                     WarehouseWorkerStatusId.MovingWorkerTrolleyToStorage or
                     WarehouseWorkerStatusId.MovingWorkerTrolleyToCustomerLoading or
                     WarehouseWorkerStatusId.ReturningWorkerTrolley))
            {
                throw new InvalidOperationException(
                    $"Leased trolley {trolley.EntityId} has an invalid worker pusher.");
            }
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
