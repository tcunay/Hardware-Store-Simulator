using System;
using HardwareStore.Gameplay.Common.Physics;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Employees
{
    public static class WorkerTrolleyLeaseUtility
    {
        private static readonly Quaternion TurnAround =
            Quaternion.Euler(0f, 180f, 0f);
        private static readonly Quaternion StorageClearanceTurn =
            Quaternion.Euler(0f, 105f, 0f);

        public static void BeginLease(GameEntity trolley, int storeEntityId,
            Pose customerLoadingPose)
        {
            if (trolley == null || trolley.isDestructed ||
                !trolley.isPlatformTrolley || trolley.isWorkerTrolley ||
                !trolley.isInteractable || !trolley.hasEntityId ||
                !trolley.hasTrolleyStoreEntityId ||
                trolley.TrolleyStoreEntityId != storeEntityId ||
                !trolley.hasTrolleyCapacity ||
                !trolley.hasOccupiedTrolleySlotCount ||
                trolley.OccupiedTrolleySlotCount != 0 ||
                !trolley.hasTrolleyFollowDistance || !trolley.hasTransform ||
                !trolley.hasRigidbody || !trolley.hasColliders ||
                !trolley.hasSlots ||
                trolley.Slots.Length != trolley.TrolleyCapacity ||
                trolley.hasTrolleyPusherEntityId ||
                trolley.hasWorkerTrolleyStoreEntityId ||
                trolley.hasWorkerTrolleyHomePosition ||
                trolley.hasWorkerTrolleyHomeRotation ||
                trolley.hasWorkerTrolleyCustomerLoadingPosition ||
                trolley.hasWorkerTrolleyCustomerLoadingRotation)
            {
                throw new InvalidOperationException(
                    "Platform trolley cannot begin a warehouse-worker lease.");
            }

            GhostMoverCollisionProfile.Apply(
                trolley.Rigidbody,
                trolley.Colliders,
                GhostMoverCollisionProfile.GhostMover);
            trolley.isInteractable = false;
            trolley.isHighlighted = false;
            trolley.AddWorkerTrolleyStoreEntityId(storeEntityId);
            trolley.AddWorkerTrolleyHomePosition(trolley.Transform.position);
            trolley.AddWorkerTrolleyHomeRotation(trolley.Transform.rotation);
            trolley.AddWorkerTrolleyCustomerLoadingPosition(
                customerLoadingPose.position);
            trolley.AddWorkerTrolleyCustomerLoadingRotation(
                customerLoadingPose.rotation);
            trolley.isWorkerTrolley = true;
        }

        public static void ReleaseLease(GameEntity trolley)
        {
            if (trolley == null || trolley.isDestructed ||
                !trolley.isPlatformTrolley || !trolley.isWorkerTrolley ||
                trolley.isInteractable || !trolley.hasEntityId ||
                !trolley.hasTrolleyStoreEntityId ||
                !trolley.hasOccupiedTrolleySlotCount ||
                trolley.OccupiedTrolleySlotCount != 0 ||
                trolley.hasTrolleyPusherEntityId ||
                !trolley.hasWorkerTrolleyStoreEntityId ||
                trolley.WorkerTrolleyStoreEntityId !=
                trolley.TrolleyStoreEntityId ||
                !trolley.hasWorkerTrolleyHomePosition ||
                !trolley.hasWorkerTrolleyHomeRotation ||
                !trolley.hasWorkerTrolleyCustomerLoadingPosition ||
                !trolley.hasWorkerTrolleyCustomerLoadingRotation)
            {
                throw new InvalidOperationException(
                    "Platform trolley cannot release its warehouse-worker lease.");
            }

            trolley.isWorkerTrolley = false;
            trolley.RemoveWorkerTrolleyStoreEntityId();
            trolley.RemoveWorkerTrolleyHomePosition();
            trolley.RemoveWorkerTrolleyHomeRotation();
            trolley.RemoveWorkerTrolleyCustomerLoadingPosition();
            trolley.RemoveWorkerTrolleyCustomerLoadingRotation();
            trolley.isHighlighted = false;
            trolley.isInteractable = true;
        }

        public static Pose CreateAccessPose(Vector3 workerAccessPosition,
            Quaternion workerAccessRotation, float followDistance)
        {
            Quaternion cartRotation = workerAccessRotation * TurnAround;
            Vector3 cartPosition = workerAccessPosition +
                cartRotation * Vector3.forward * followDistance;
            return new Pose(cartPosition, cartRotation);
        }

        public static Pose CreateStorageAccessPose(Vector3 workerAccessPosition,
            Quaternion workerAccessRotation, float followDistance)
        {
            Quaternion cartRotation =
                workerAccessRotation * StorageClearanceTurn;
            Vector3 cartPosition = workerAccessPosition +
                cartRotation * Vector3.forward * followDistance;
            return new Pose(cartPosition, cartRotation);
        }

        public static Vector3 GetPusherPosition(GameEntity trolley,
            Pose cartPose) =>
            cartPose.position - cartPose.rotation * Vector3.forward *
            trolley.TrolleyFollowDistance;
    }
}
