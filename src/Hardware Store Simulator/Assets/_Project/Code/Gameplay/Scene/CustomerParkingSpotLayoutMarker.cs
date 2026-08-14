using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardwareStore.Gameplay.Scene
{
    [DisallowMultipleComponent]
    public sealed class CustomerParkingSpotLayoutMarker : MonoBehaviour
    {
        [SerializeField, Min(0)] private int _index;
        [SerializeField] private Transform[] _vehicleArrivalRoute;
        [SerializeField] private Transform[] _vehicleToLoadingRoute;
        [SerializeField] private Transform[] _vehicleParkingDepartureRoute;
        [SerializeField] private Transform[] _customerApproachRoute;
        [SerializeField] private Transform[] _customerReturnRoute;

        public int Index => _index;

        public CustomerParkingSpotSceneLayout Layout =>
            new(
                _index,
                CreateRouteSnapshot(_vehicleArrivalRoute, nameof(_vehicleArrivalRoute)),
                CreateRouteSnapshot(_vehicleToLoadingRoute, nameof(_vehicleToLoadingRoute)),
                CreateRouteSnapshot(
                    _vehicleParkingDepartureRoute,
                    nameof(_vehicleParkingDepartureRoute)),
                CreateRouteSnapshot(_customerApproachRoute, nameof(_customerApproachRoute)),
                CreateRouteSnapshot(_customerReturnRoute, nameof(_customerReturnRoute)));

        public void Configure(
            int index,
            Transform[] vehicleArrivalRoute,
            Transform[] vehicleToLoadingRoute,
            Transform[] vehicleParkingDepartureRoute,
            Transform[] customerApproachRoute,
            Transform[] customerReturnRoute)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            ValidateRoute(vehicleArrivalRoute, nameof(vehicleArrivalRoute));
            ValidateRoute(vehicleToLoadingRoute, nameof(vehicleToLoadingRoute));
            ValidateRoute(
                vehicleParkingDepartureRoute,
                nameof(vehicleParkingDepartureRoute));
            ValidateRoute(customerApproachRoute, nameof(customerApproachRoute));
            ValidateRoute(customerReturnRoute, nameof(customerReturnRoute));
            _index = index;
            _vehicleArrivalRoute = (Transform[])vehicleArrivalRoute.Clone();
            _vehicleToLoadingRoute = (Transform[])vehicleToLoadingRoute.Clone();
            _vehicleParkingDepartureRoute =
                (Transform[])vehicleParkingDepartureRoute.Clone();
            _customerApproachRoute = (Transform[])customerApproachRoute.Clone();
            _customerReturnRoute = (Transform[])customerReturnRoute.Clone();
        }

        private static Pose[] CreateRouteSnapshot(Transform[] waypoints, string routeName)
        {
            ValidateRoute(waypoints, routeName);
            var poses = new Pose[waypoints.Length];
            for (int index = 0; index < waypoints.Length; index++)
            {
                Transform waypoint = waypoints[index];
                poses[index] = new Pose(waypoint.position, waypoint.rotation);
            }
            return poses;
        }

        private static void ValidateRoute(Transform[] waypoints, string routeName)
        {
            if (waypoints == null)
                throw new ArgumentNullException(routeName);
            if (waypoints.Length < 2)
                throw new ArgumentException(
                    "A customer flow route must contain at least two waypoints.",
                    routeName);

            var uniqueWaypoints = new HashSet<Transform>();
            for (int index = 0; index < waypoints.Length; index++)
            {
                Transform waypoint = waypoints[index];
                if (waypoint == null)
                    throw new ArgumentException($"Route waypoint {index} is missing.", routeName);
                if (!uniqueWaypoints.Add(waypoint))
                {
                    throw new ArgumentException(
                        $"Route waypoint {index} is registered more than once.",
                        routeName);
                }
            }
        }
    }
}
