using System;
using UnityEngine;

namespace HardwareStore.Gameplay.Scene
{
    public sealed class CustomerParkingSpotSceneLayout
    {
        private readonly Pose[] _vehicleArrivalRoute;
        private readonly Pose[] _vehicleToLoadingRoute;
        private readonly Pose[] _vehicleParkingDepartureRoute;
        private readonly Pose[] _customerApproachRoute;
        private readonly Pose[] _customerReturnRoute;

        public CustomerParkingSpotSceneLayout(
            int index,
            Pose[] vehicleArrivalRoute,
            Pose[] vehicleToLoadingRoute,
            Pose[] vehicleParkingDepartureRoute,
            Pose[] customerApproachRoute,
            Pose[] customerReturnRoute)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            Index = index;
            _vehicleArrivalRoute = CloneRoute(
                vehicleArrivalRoute,
                nameof(vehicleArrivalRoute));
            _vehicleToLoadingRoute = CloneRoute(
                vehicleToLoadingRoute,
                nameof(vehicleToLoadingRoute));
            _vehicleParkingDepartureRoute = CloneRoute(
                vehicleParkingDepartureRoute,
                nameof(vehicleParkingDepartureRoute));
            _customerApproachRoute = CloneRoute(
                customerApproachRoute,
                nameof(customerApproachRoute));
            _customerReturnRoute = CloneRoute(
                customerReturnRoute,
                nameof(customerReturnRoute));
        }

        public int Index { get; }
        public Pose[] VehicleArrivalRoute => (Pose[])_vehicleArrivalRoute.Clone();
        public Pose[] VehicleToLoadingRoute => (Pose[])_vehicleToLoadingRoute.Clone();
        public Pose[] VehicleParkingDepartureRoute =>
            (Pose[])_vehicleParkingDepartureRoute.Clone();
        public Pose[] CustomerApproachRoute => (Pose[])_customerApproachRoute.Clone();
        public Pose[] CustomerReturnRoute => (Pose[])_customerReturnRoute.Clone();

        public CustomerParkingSpotSceneLayout Clone() =>
            new(
                Index,
                VehicleArrivalRoute,
                VehicleToLoadingRoute,
                VehicleParkingDepartureRoute,
                CustomerApproachRoute,
                CustomerReturnRoute);

        private static Pose[] CloneRoute(Pose[] route, string argumentName)
        {
            if (route == null)
                throw new ArgumentNullException(argumentName);
            if (route.Length < 2)
                throw new ArgumentException(
                    "A customer flow route must contain at least two poses.",
                    argumentName);
            for (int index = 0; index < route.Length; index++)
            {
                Pose pose = route[index];
                if (!IsFinite(pose.position) || !IsFinite(pose.rotation))
                {
                    throw new ArgumentException(
                        $"Customer flow route pose {index} must be finite.",
                        argumentName);
                }
            }
            return (Pose[])route.Clone();
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            IsFinite(value.x) && IsFinite(value.y) &&
            IsFinite(value.z) && IsFinite(value.w);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
