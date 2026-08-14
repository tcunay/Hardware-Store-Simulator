using System;
using UnityEngine;

namespace HardwareStore.Gameplay.Scene
{
    public sealed class CustomerFlowSceneLayout
    {
        private readonly CustomerParkingSpotSceneLayout[] _parkingSpots;
        private readonly Pose[] _queuePoses;
        private readonly Pose[] _queueAbandonExitRoute;
        private readonly Pose[] _loadingDepartureRoute;

        public CustomerFlowSceneLayout(
            CustomerParkingSpotSceneLayout[] parkingSpots,
            Pose[] queuePoses,
            Pose[] queueAbandonExitRoute,
            Pose[] loadingDepartureRoute)
        {
            if (parkingSpots == null)
                throw new ArgumentNullException(nameof(parkingSpots));
            if (queuePoses == null)
                throw new ArgumentNullException(nameof(queuePoses));
            if (queueAbandonExitRoute == null)
                throw new ArgumentNullException(nameof(queueAbandonExitRoute));
            if (loadingDepartureRoute == null)
                throw new ArgumentNullException(nameof(loadingDepartureRoute));
            if (parkingSpots.Length == 0)
                throw new ArgumentException(
                    "A customer flow layout must contain at least one parking spot.",
                    nameof(parkingSpots));
            if (queuePoses.Length == 0)
                throw new ArgumentException(
                    "A customer flow layout must contain at least one queue pose.",
                    nameof(queuePoses));
            if (queueAbandonExitRoute.Length != queuePoses.Length + 1)
                throw new ArgumentException(
                    "A customer queue abandon exit route must contain one lateral exit " +
                    "pose per queue pose followed by one shared return-route join pose.",
                    nameof(queueAbandonExitRoute));
            if (loadingDepartureRoute.Length < 2)
                throw new ArgumentException(
                    "A customer loading departure route must contain at least two poses.",
                    nameof(loadingDepartureRoute));

            _parkingSpots = new CustomerParkingSpotSceneLayout[parkingSpots.Length];
            for (int index = 0; index < parkingSpots.Length; index++)
            {
                CustomerParkingSpotSceneLayout parkingSpot = parkingSpots[index] ??
                    throw new ArgumentException(
                        $"Customer parking spot layout {index} is missing.",
                        nameof(parkingSpots));
                if (parkingSpot.Index != index)
                {
                    throw new ArgumentException(
                        $"Customer parking spot layout {index} has non-contiguous index " +
                        $"{parkingSpot.Index}.",
                        nameof(parkingSpots));
                }

                _parkingSpots[index] = parkingSpot.Clone();
            }

            _queuePoses = (Pose[])queuePoses.Clone();
            _queueAbandonExitRoute = (Pose[])queueAbandonExitRoute.Clone();
            _loadingDepartureRoute = (Pose[])loadingDepartureRoute.Clone();
            ValidateContinuity();
        }

        public CustomerParkingSpotSceneLayout[] ParkingSpots
        {
            get
            {
                var clone = new CustomerParkingSpotSceneLayout[_parkingSpots.Length];
                for (int index = 0; index < _parkingSpots.Length; index++)
                    clone[index] = _parkingSpots[index].Clone();
                return clone;
            }
        }

        public Pose[] QueuePoses => (Pose[])_queuePoses.Clone();
        public Pose[] QueueAbandonExitRoute => (Pose[])_queueAbandonExitRoute.Clone();
        public Pose[] LoadingDepartureRoute => (Pose[])_loadingDepartureRoute.Clone();

        public CustomerFlowSceneLayout Clone() =>
            new(ParkingSpots, QueuePoses, QueueAbandonExitRoute, LoadingDepartureRoute);

        private void ValidateContinuity()
        {
            for (int queueIndex = 0; queueIndex < _queuePoses.Length; queueIndex++)
            {
                Pose queuePose = _queuePoses[queueIndex];
                if (!IsFinite(queuePose.position) || !IsFinite(queuePose.rotation))
                {
                    throw new ArgumentException(
                        $"Customer queue pose {queueIndex} must be finite.",
                        "queuePoses");
                }
            }

            Vector3 exitOffset =
                _queueAbandonExitRoute[0].position - _queuePoses[0].position;
            if (!IsFinite(exitOffset) || exitOffset.sqrMagnitude < 0.000001f ||
                Mathf.Abs(exitOffset.y) >= 0.001f)
            {
                throw new ArgumentException(
                    "Customer queue abandon exits must define a non-zero lateral offset " +
                    "on the queue walking plane.",
                    "queueAbandonExitRoute");
            }
            for (int routeIndex = 0;
                 routeIndex < _queueAbandonExitRoute.Length;
                 routeIndex++)
            {
                Pose routePose = _queueAbandonExitRoute[routeIndex];
                if (!IsFinite(routePose.position) || !IsFinite(routePose.rotation))
                {
                    throw new ArgumentException(
                        $"Customer queue abandon exit pose {routeIndex} must be finite.",
                        "queueAbandonExitRoute");
                }
                if (routeIndex < _queuePoses.Length &&
                    Vector3.Distance(
                        routePose.position - _queuePoses[routeIndex].position,
                        exitOffset) >= 0.001f)
                {
                    throw new ArgumentException(
                        $"Customer queue abandon exit pose {routeIndex} must preserve " +
                        "the authored lateral alignment with its queue pose.",
                        "queueAbandonExitRoute");
                }
            }

            for (int routeIndex = 0;
                 routeIndex < _loadingDepartureRoute.Length;
                 routeIndex++)
            {
                Pose routePose = _loadingDepartureRoute[routeIndex];
                if (!IsFinite(routePose.position) || !IsFinite(routePose.rotation))
                {
                    throw new ArgumentException(
                        $"Customer loading departure pose {routeIndex} must be finite.",
                        "loadingDepartureRoute");
                }
            }

            foreach (CustomerParkingSpotSceneLayout parkingSpot in _parkingSpots)
            {
                Pose[] arrival = parkingSpot.VehicleArrivalRoute;
                Pose[] toLoading = parkingSpot.VehicleToLoadingRoute;
                Pose[] parkingDeparture = parkingSpot.VehicleParkingDepartureRoute;
                Pose[] approach = parkingSpot.CustomerApproachRoute;
                Pose[] returning = parkingSpot.CustomerReturnRoute;
                if (!Matches(arrival[^1], toLoading[0]))
                {
                    throw new ArgumentException(
                        $"Customer parking spot {parkingSpot.Index} arrival and loading " +
                        "routes do not join at the parking pose.",
                        "parkingSpots");
                }
                if (!Matches(arrival[^1], parkingDeparture[0]))
                {
                    throw new ArgumentException(
                        $"Customer parking spot {parkingSpot.Index} arrival and parking " +
                        "departure routes do not join at the parking pose.",
                        "parkingSpots");
                }
                if (!Matches(toLoading[^1], _loadingDepartureRoute[0]))
                {
                    throw new ArgumentException(
                        $"Customer parking spot {parkingSpot.Index} does not enter the " +
                        "shared loading bay at its departure pose.",
                        "parkingSpots");
                }
                if (!Matches(approach[^1], _queuePoses[^1]) ||
                    !Matches(returning[0], _queuePoses[0]) ||
                    !Matches(_queueAbandonExitRoute[^1], returning[1]) ||
                    !Matches(returning[^1], approach[0]))
                {
                    throw new ArgumentException(
                        $"Customer parking spot {parkingSpot.Index} pedestrian routes do not " +
                        "join the queue tail, service pose, abandon exit and vehicle door.",
                        "parkingSpots");
                }
            }
        }

        private static bool Matches(Pose first, Pose second) =>
            Vector3.Distance(first.position, second.position) < 0.001f &&
            Quaternion.Angle(first.rotation, second.rotation) < 0.01f;

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            IsFinite(value.x) && IsFinite(value.y) &&
            IsFinite(value.z) && IsFinite(value.w);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
