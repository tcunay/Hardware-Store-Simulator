using System;
using Gley.TrafficSystem;
using HardwareStore.Gameplay.Configs;
using UnityEngine;
using VehicleTypes = Gley.TrafficSystem.User.VehicleTypes;

namespace HardwareStore.Infrastructure.VehicleTraffic.Gley
{
    [CreateAssetMenu(fileName = "GleyTrafficConfig",
        menuName = "Hardware Store/Infrastructure/Gley Traffic Config")]
    public sealed class GleyTrafficConfig : ScriptableObject, IValidatableConfig
    {
        [Header("Pool")]
        [SerializeField] private VehiclePool _vehiclePool;
        [SerializeField, Min(1)] private int _maximumVehicleCount = 8;

        [Header("Simulation")]
        [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f;
        [SerializeField, Min(1)] private int _activeSquaresLevel = 1;
        [SerializeField, Min(1)] private int _defaultPathLength = 8;
        [SerializeField, Min(1f)] private float _spawnRequestTimeout = 20f;
        [SerializeField] private bool _useWaypointPriority = true;
        [SerializeField] private bool _lightsOn;

        [Header("Placement")]
        [SerializeField] private float _minimumDistanceToAdd = -1f;
        [SerializeField] private float _distanceToRemove = -1f;
        [SerializeField] private float _minimumLaneOffset = -0.15f;
        [SerializeField] private float _maximumLaneOffset = 0.15f;

        internal VehiclePool VehiclePool => _vehiclePool;
        internal int MaximumVehicleCount => _maximumVehicleCount;
        internal float SpawnRequestTimeout => _spawnRequestTimeout;

        public void Validate()
        {
            const string owner = nameof(GleyTrafficConfig);
            if (_vehiclePool == null)
                throw Invalid(owner, nameof(_vehiclePool), "must reference a vehicle pool");
            if (_vehiclePool.trafficCars == null || _vehiclePool.trafficCars.Length == 0)
                throw Invalid(owner, nameof(_vehiclePool), "must contain at least one vehicle type");
            if (_maximumVehicleCount < _vehiclePool.trafficCars.Length)
            {
                throw Invalid(owner, nameof(_maximumVehicleCount),
                    "must be greater than or equal to the number of vehicle types");
            }

            for (int i = 0; i < _vehiclePool.trafficCars.Length; i++)
            {
                CarType carType = _vehiclePool.trafficCars[i];
                if (carType == null || carType.VehiclePrefab == null)
                    throw Invalid(owner, nameof(_vehiclePool), $"contains an empty vehicle at index {i}");
                if (!carType.Ignore)
                {
                    throw Invalid(owner, nameof(_vehiclePool),
                        $"vehicle at index {i} must be ignored to prevent autonomous density spawning");
                }

                VehicleComponent vehicle = carType.VehiclePrefab.GetComponent<VehicleComponent>();
                if (vehicle == null)
                {
                    throw Invalid(owner, nameof(_vehiclePool),
                        $"vehicle at index {i} has no {nameof(VehicleComponent)}");
                }
                if (vehicle.VehicleType != VehicleTypes.Car)
                {
                    throw Invalid(owner, nameof(_vehiclePool),
                        $"vehicle at index {i} must use {VehicleTypes.Car}");
                }
            }

            RequirePositive(_maximumVehicleCount, owner, nameof(_maximumVehicleCount));
            RequireInRange(_masterVolume, 0f, 1f, owner, nameof(_masterVolume));
            RequirePositive(_activeSquaresLevel, owner, nameof(_activeSquaresLevel));
            RequirePositive(_defaultPathLength, owner, nameof(_defaultPathLength));
            RequirePositive(_spawnRequestTimeout, owner, nameof(_spawnRequestTimeout));
            RequireDefaultOrNonNegative(
                _minimumDistanceToAdd, owner, nameof(_minimumDistanceToAdd));
            RequireDefaultOrNonNegative(
                _distanceToRemove, owner, nameof(_distanceToRemove));
            RequireFinite(_minimumLaneOffset, owner, nameof(_minimumLaneOffset));
            RequireFinite(_maximumLaneOffset, owner, nameof(_maximumLaneOffset));
            if (_minimumLaneOffset > _maximumLaneOffset)
            {
                throw Invalid(owner, nameof(_minimumLaneOffset),
                    $"must be less than or equal to {nameof(_maximumLaneOffset)}");
            }
        }

        internal TrafficOptions CreateOptions() => new()
        {
            InitialDensity = 0,
            MasterVolume = _masterVolume,
            ActiveSquaresLevel = _activeSquaresLevel,
            DefaultPathLength = _defaultPathLength,
            UseWaypointPriority = _useWaypointPriority,
            LightsOn = _lightsOn,
            MinDistanceToAdd = _minimumDistanceToAdd,
            DistanceToRemove = _distanceToRemove,
            MinOffset = _minimumLaneOffset,
            MaxOffset = _maximumLaneOffset,
        };

        private static void RequirePositive(int value, string owner, string property)
        {
            if (value <= 0)
                throw Invalid(owner, property, "must be greater than zero");
        }

        private static void RequirePositive(float value, string owner, string property)
        {
            RequireFinite(value, owner, property);
            if (value <= 0f)
                throw Invalid(owner, property, "must be greater than zero");
        }

        private static void RequireDefaultOrNonNegative(float value, string owner,
            string property)
        {
            RequireFinite(value, owner, property);
            if (value < 0f && value != -1f)
                throw Invalid(owner, property, "must be -1 or non-negative");
        }

        private static void RequireInRange(float value, float minimum, float maximum,
            string owner, string property)
        {
            RequireFinite(value, owner, property);
            if (value < minimum || value > maximum)
                throw Invalid(owner, property, $"must be in range [{minimum}, {maximum}]");
        }

        private static void RequireFinite(float value, string owner, string property)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw Invalid(owner, property, "must be finite");
        }

        private static InvalidOperationException Invalid(string owner, string property,
            string requirement) => new($"{owner}.{property} {requirement}.");
    }
}
