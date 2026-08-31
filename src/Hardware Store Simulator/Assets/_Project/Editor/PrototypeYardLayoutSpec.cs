using System;
using UnityEngine;

namespace HardwareStore.Editor
{
    /// <summary>
    /// Editor-only source of truth for the rebuildable prototype-yard blockout.
    /// Gameplay consumes the typed scene markers produced from this specification.
    /// </summary>
    internal static class PrototypeYardLayoutSpec
    {
        public const float PathY = 0.02f;
        public const float YardHalfWidth = 20f;
        public const float YardFrontZ = -16f;
        public const float YardRearZ = 60f;
        public const float PublicRoadCenterZ = -29f;
        public const float PublicTrafficLaneZ = -31f;
        public const float ParallelParkingZ = -23f;
        public const float CustomerEntryX = 13.5f;
        public const float CustomerExitX = 5f;
        public const float CustomerLoadingX = 5f;
        public const float CustomerLoadingZ = 10f;
        public const int ActiveParkingCount = 3;
        public const int VisualParkingCount = 10;

        public static readonly Vector3 StorageOffset = new(-8.5f, 0f, 8.5f);
        public static readonly Vector3 LumberOffset = new(-16f, 0f, 18f);
        public static readonly Pose DeliveryVehiclePose = new(
            Point(-8.5f, 31f), Quaternion.identity);
        public static readonly Pose FreightTruckPose = new(
            Point(6f, 47f), Quaternion.Euler(0f, 180f, 0f));
        public static readonly Pose WarehouseWorkerIdlePose = new(
            StorageOffset + new Vector3(-0.5f, PathY, 0f),
            Quaternion.Euler(0f, -90f, 0f));
        public static readonly Pose WarehouseWorkerDeliveryAccessPose = new(
            DeliveryVehiclePose.position + new Vector3(-1.85f, 0f, -0.85f),
            Quaternion.Euler(0f, 90f, 0f));
        public static readonly Pose WarehouseWorkerStorageAccessPose = new(
            StorageOffset + new Vector3(5f, PathY, 2.45f),
            Quaternion.identity);
        public static readonly Pose WarehouseWorkerCustomerLoadingAccessPose = new(
            Point(CustomerLoadingX, 14.12f),
            Quaternion.Euler(0f, 180f, 0f));
        public static readonly Pose PlatformTrolleySpawnPose = new(
            StorageOffset + new Vector3(-2.2f, PathY, 0f),
            Quaternion.Euler(0f, -90f, 0f));
        public static readonly Pose WorkerTrolleyHomePose =
            PlatformTrolleySpawnPose;
        public static readonly Pose WorkerTrolleyCustomerLoadingAccessPose = new(
            Point(CustomerLoadingX, 14.8f),
            Quaternion.Euler(0f, 180f, 0f));
        public static readonly Pose WorkerInboundTrolleyStorageBypassPose = new(
            StorageOffset + new Vector3(8.3f, PathY, -1.5f),
            Quaternion.Euler(0f, -90f, 0f));
        public static readonly Pose WorkerInboundTrolleyStorageAccessPose = new(
            StorageOffset + new Vector3(5.22f, PathY, 0.5f),
            Quaternion.Euler(0f, -90f, 0f));
        public static readonly Pose WorkerOutboundTrolleyStorageApproachPose = new(
            StorageOffset + new Vector3(10f, PathY, 1.95f),
            Quaternion.Euler(0f, -90f, 0f));
        public static readonly Pose WorkerOutboundTrolleyStorageAccessPose = new(
            StorageOffset + new Vector3(3.6f, PathY, 1.95f),
            Quaternion.Euler(0f, -90f, 0f));

        private static readonly float[] VisualParkingXs =
        {
            -31.05f, -24.15f, -17.25f, -10.35f, -3.45f,
              3.45f,  10.35f,  17.25f,  24.15f, 31.05f
        };

        private static readonly int[] ActiveVisualParkingIndices = { 1, 3, 5 };

        public static Vector3 Point(float x, float z) => new(x, PathY, z);

        public static float GetVisualParkingX(int index)
        {
            if (index < 0 || index >= VisualParkingXs.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            return VisualParkingXs[index];
        }

        public static float GetActiveParkingX(int index)
        {
            if (index < 0 || index >= ActiveVisualParkingIndices.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            return GetVisualParkingX(ActiveVisualParkingIndices[index]);
        }

        public static int GetActiveVisualParkingIndex(int index)
        {
            if (index < 0 || index >= ActiveVisualParkingIndices.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            return ActiveVisualParkingIndices[index];
        }

        public static Vector3[] BuildArrivalVisualPositions(float parkingX) =>
            new[]
            {
                Point(-42f, PublicTrafficLaneZ),
                Point(parkingX - 11f, PublicTrafficLaneZ),
                Point(parkingX - 8f, -30.25f),
                Point(parkingX - 5.8f, -28.2f),
                Point(parkingX - 3.6f, -25.3f),
                Point(parkingX - 1.8f, ParallelParkingZ),
                Point(parkingX, ParallelParkingZ)
            };

        public static Vector3[] BuildToLoadingVisualPositions(float parkingX) =>
            new[]
            {
                Point(parkingX, ParallelParkingZ),
                Point(parkingX + 2f, ParallelParkingZ),
                Point(parkingX + 4.2f, -24f),
                Point(parkingX + 6.2f, -26.2f),
                Point(parkingX + 8f, -28.5f),
                Point(13f, PublicTrafficLaneZ),
                Point(13.2f, -30.5f),
                Point(14.5f, -28f),
                Point(14.5f, -24f),
                Point(CustomerEntryX, -20f),
                Point(CustomerEntryX, YardFrontZ),
                Point(CustomerEntryX, -8f),
                Point(CustomerEntryX, 0f),
                Point(CustomerEntryX, 8f),
                Point(CustomerEntryX, 16f),
                Point(CustomerEntryX, 20f),
                Point(13f, 24f),
                Point(11f, 28f),
                Point(8f, 31f),
                Point(5.5f, 31f),
                Point(4.2f, 29f),
                Point(4f, 25f),
                Point(4.3f, 21f),
                Point(CustomerLoadingX, 18f),
                Point(CustomerLoadingX, 16f),
                Point(CustomerLoadingX, 14f),
                Point(CustomerLoadingX, CustomerLoadingZ)
            };

        public static Vector3[] BuildParkingDepartureVisualPositions(float parkingX) =>
            new[]
            {
                Point(parkingX, ParallelParkingZ),
                Point(parkingX + 2f, ParallelParkingZ),
                Point(parkingX + 4.2f, -24f),
                Point(parkingX + 6.2f, -26.2f),
                Point(parkingX + 8f, -28.5f),
                Point(13f, PublicTrafficLaneZ),
                Point(22f, PublicTrafficLaneZ),
                Point(42f, PublicTrafficLaneZ)
            };

        public static Vector3[] BuildLoadingDepartureVisualPositions() =>
            new[]
            {
                Point(CustomerLoadingX, CustomerLoadingZ),
                Point(CustomerExitX, 6f),
                Point(CustomerExitX, 1f),
                Point(CustomerExitX, -5f),
                Point(CustomerExitX, -10f),
                Point(CustomerExitX, YardFrontZ),
                Point(5.3f, -19f),
                Point(6.3f, -22f),
                Point(8f, -26f),
                Point(10.5f, -29f),
                Point(13f, PublicTrafficLaneZ),
                Point(22f, PublicTrafficLaneZ),
                Point(42f, PublicTrafficLaneZ)
            };

        public static Pose[] BuildQueuePoses() =>
            new[]
            {
                new Pose(Point(-7.25f, 0.55f), Quaternion.identity),
                new Pose(Point(-7.25f, -0.75f), Quaternion.identity),
                new Pose(Point(-7.25f, -2.05f), Quaternion.identity)
            };

        public static Pose[] BuildQueueAbandonExitRoute() =>
            new[]
            {
                new Pose(Point(-8f, 0.55f), Quaternion.Euler(0f, 180f, 0f)),
                new Pose(Point(-8f, -0.75f), Quaternion.Euler(0f, 180f, 0f)),
                new Pose(Point(-8f, -2.05f), Quaternion.Euler(0f, 180f, 0f)),
                new Pose(Point(-8f, -3f), Quaternion.Euler(0f, 180f, 0f))
            };

        public static Pose BuildParkingPose(float parkingX) =>
            new(Point(parkingX, ParallelParkingZ), Quaternion.Euler(0f, 90f, 0f));

        public static Pose BuildCustomerDoorPose(float parkingX) =>
            new(Point(parkingX + 0.7f, -20.75f), Quaternion.identity);

        public static Pose[] BuildCustomerApproachRoute(
            float parkingX,
            Pose queueTail)
        {
            Pose doorPose = BuildCustomerDoorPose(parkingX);
            return new[]
            {
                doorPose,
                new Pose(Point(parkingX + 0.7f, -18.5f), Quaternion.identity),
                new Pose(Point(-7.25f, -17.25f), Quaternion.identity),
                new Pose(Point(-7.25f, -14.6f), Quaternion.identity),
                new Pose(Point(-7.25f, -3f), Quaternion.identity),
                queueTail
            };
        }

        public static Pose[] BuildCustomerReturnRoute(
            float parkingX,
            Pose queueService,
            Pose queueAbandonJoin)
        {
            Pose doorPose = BuildCustomerDoorPose(parkingX);
            return new[]
            {
                queueService,
                queueAbandonJoin,
                new Pose(Point(-7.25f, -14.6f), Quaternion.Euler(0f, 180f, 0f)),
                new Pose(Point(-7.25f, -17.25f), Quaternion.Euler(0f, 180f, 0f)),
                new Pose(Point(parkingX + 0.7f, -18.5f),
                    Quaternion.Euler(0f, 180f, 0f)),
                doorPose
            };
        }
    }
}
