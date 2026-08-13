using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HardwareStore.Gameplay.Scene
{
    [DisallowMultipleComponent]
    public sealed class CustomerFlowLayoutMarker : MonoBehaviour
    {
        [SerializeField] private CustomerParkingSpotLayoutMarker[] _parkingSpots;
        [SerializeField] private Transform[] _queuePoses;
        [SerializeField] private Transform[] _loadingDepartureRoute;

        public CustomerFlowSceneLayout Layout => CreateSnapshot();

        public void Configure(
            CustomerParkingSpotLayoutMarker[] parkingSpots,
            Transform[] queuePoses,
            Transform[] loadingDepartureRoute)
        {
            ValidateParkingSpots(parkingSpots);
            ValidateWaypoints(queuePoses, nameof(queuePoses), minimumCount: 1);
            ValidateWaypoints(
                loadingDepartureRoute,
                nameof(loadingDepartureRoute),
                minimumCount: 2);
            _parkingSpots = (CustomerParkingSpotLayoutMarker[])parkingSpots.Clone();
            _queuePoses = (Transform[])queuePoses.Clone();
            _loadingDepartureRoute = (Transform[])loadingDepartureRoute.Clone();
        }

        private CustomerFlowSceneLayout CreateSnapshot()
        {
            ValidateParkingSpots(_parkingSpots);
            ValidateWaypoints(_queuePoses, nameof(_queuePoses), minimumCount: 1);
            ValidateWaypoints(
                _loadingDepartureRoute,
                nameof(_loadingDepartureRoute),
                minimumCount: 2);

            CustomerParkingSpotSceneLayout[] parkingSpots = _parkingSpots
                .OrderBy(marker => marker.Index)
                .Select(marker => marker.Layout)
                .ToArray();
            return new CustomerFlowSceneLayout(
                parkingSpots,
                CreatePoseSnapshot(_queuePoses),
                CreatePoseSnapshot(_loadingDepartureRoute));
        }

        private static void ValidateParkingSpots(
            CustomerParkingSpotLayoutMarker[] parkingSpots)
        {
            if (parkingSpots == null)
                throw new ArgumentNullException(nameof(parkingSpots));
            if (parkingSpots.Length == 0)
                throw new ArgumentException(
                    "A customer flow layout must contain at least one parking spot marker.",
                    nameof(parkingSpots));

            var uniqueMarkers = new HashSet<CustomerParkingSpotLayoutMarker>();
            var uniqueIndices = new HashSet<int>();
            for (int index = 0; index < parkingSpots.Length; index++)
            {
                CustomerParkingSpotLayoutMarker marker = parkingSpots[index];
                if (marker == null)
                    throw new ArgumentException(
                        $"Customer parking spot marker {index} is missing.",
                        nameof(parkingSpots));
                if (!uniqueMarkers.Add(marker) || !uniqueIndices.Add(marker.Index))
                {
                    throw new ArgumentException(
                        $"Customer parking spot marker or index {marker.Index} is duplicated.",
                        nameof(parkingSpots));
                }
            }

            for (int index = 0; index < parkingSpots.Length; index++)
            {
                if (!uniqueIndices.Contains(index))
                {
                    throw new ArgumentException(
                        $"Customer parking spot indices must be contiguous from zero; " +
                        $"index {index} is missing.",
                        nameof(parkingSpots));
                }
            }
        }

        private static void ValidateWaypoints(
            Transform[] waypoints,
            string argumentName,
            int minimumCount)
        {
            if (waypoints == null)
                throw new ArgumentNullException(argumentName);
            if (waypoints.Length < minimumCount)
            {
                throw new ArgumentException(
                    $"Customer flow waypoints must contain at least {minimumCount} entries.",
                    argumentName);
            }

            var uniqueWaypoints = new HashSet<Transform>();
            for (int index = 0; index < waypoints.Length; index++)
            {
                Transform waypoint = waypoints[index];
                if (waypoint == null)
                    throw new ArgumentException($"Waypoint {index} is missing.", argumentName);
                if (!uniqueWaypoints.Add(waypoint))
                    throw new ArgumentException($"Waypoint {index} is duplicated.", argumentName);
            }
        }

        private static Pose[] CreatePoseSnapshot(Transform[] waypoints)
        {
            var poses = new Pose[waypoints.Length];
            for (int index = 0; index < waypoints.Length; index++)
            {
                Transform waypoint = waypoints[index];
                poses[index] = new Pose(waypoint.position, waypoint.rotation);
            }
            return poses;
        }
    }
}
