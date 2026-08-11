using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "CustomerVehicleConfig",
        menuName = "Hardware Store/Gameplay/Customer Vehicle Config")]
    public sealed class CustomerVehicleConfig : ScriptableObject, IValidatableConfig
    {
        [Header("View")]
        [SerializeField] private EntityBehaviour _viewPrefab;

        [Header("Movement")]
        [SerializeField, Min(0.01f)] private float _arrivalSpeed = 4f;
        [SerializeField, Min(0.01f)] private float _departureSpeed = 5.25f;
        [SerializeField, Min(0.01f)] private float _rotationSpeed = 135f;
        [SerializeField, Min(0.001f)] private float _waypointTolerance = 0.08f;

        [Header("Schedule")]
        [SerializeField, Min(0f)] private float _completedDwellDuration = 1.25f;
        [SerializeField, Min(0f)] private float _firstCustomerDelay = 1f;
        [SerializeField, Min(0f)] private float _nextCustomerDelay = 4f;

        [Header("Cargo")]
        [SerializeField, Min(1)] private int _cargoCapacity = 3;

        public EntityBehaviour ViewPrefab => _viewPrefab;
        public float ArrivalSpeed => _arrivalSpeed;
        public float DepartureSpeed => _departureSpeed;
        public float RotationSpeed => _rotationSpeed;
        public float WaypointTolerance => _waypointTolerance;
        public float CompletedDwellDuration => _completedDwellDuration;
        public float FirstCustomerDelay => _firstCustomerDelay;
        public float NextCustomerDelay => _nextCustomerDelay;
        public int CargoCapacity => _cargoCapacity;

        public void Configure(EntityBehaviour viewPrefab, float arrivalSpeed, float departureSpeed,
            float rotationSpeed, float waypointTolerance, float completedDwellDuration,
            float firstCustomerDelay, float nextCustomerDelay, int cargoCapacity)
        {
            _viewPrefab = viewPrefab;
            _arrivalSpeed = arrivalSpeed;
            _departureSpeed = departureSpeed;
            _rotationSpeed = rotationSpeed;
            _waypointTolerance = waypointTolerance;
            _completedDwellDuration = completedDwellDuration;
            _firstCustomerDelay = firstCustomerDelay;
            _nextCustomerDelay = nextCustomerDelay;
            _cargoCapacity = cargoCapacity;

            Validate();
        }

        public void Validate()
        {
            const string owner = nameof(CustomerVehicleConfig);
            ConfigValidation.RequireReference(_viewPrefab, owner, nameof(ViewPrefab));
            ConfigValidation.RequirePositive(_arrivalSpeed, owner, nameof(ArrivalSpeed));
            ConfigValidation.RequirePositive(_departureSpeed, owner, nameof(DepartureSpeed));
            ConfigValidation.RequirePositive(_rotationSpeed, owner, nameof(RotationSpeed));
            ConfigValidation.RequirePositive(
                _waypointTolerance,
                owner,
                nameof(WaypointTolerance));
            ConfigValidation.RequireNonNegative(
                _completedDwellDuration,
                owner,
                nameof(CompletedDwellDuration));
            ConfigValidation.RequireNonNegative(
                _firstCustomerDelay,
                owner,
                nameof(FirstCustomerDelay));
            ConfigValidation.RequireNonNegative(
                _nextCustomerDelay,
                owner,
                nameof(NextCustomerDelay));
            ConfigValidation.RequirePositive(_cargoCapacity, owner, nameof(CargoCapacity));
        }
    }
}
