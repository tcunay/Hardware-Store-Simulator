using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "WarehouseWorkerConfig",
        menuName = "Hardware Store/Gameplay/Warehouse Worker Config")]
    public sealed class WarehouseWorkerConfig : ScriptableObject, IValidatableConfig
    {
        [Header("View")]
        [SerializeField] private EntityBehaviour _viewPrefab;
        [SerializeField] private EntityBehaviour _trolleyViewPrefab;

        [Header("Progression")]
        [SerializeField, Min(1)] private int _requiredCompletedOrderCount = 4;
        [SerializeField, Min(1)] private int _hirePrice = 400;
        [SerializeField, Min(1)] private int _dailyWage = 100;

        [Header("Movement")]
        [SerializeField, Min(0.01f)] private float _movementSpeed = 2.8f;
        [SerializeField, Min(0.01f)] private float _acceleration = 12f;
        [SerializeField, Min(0.01f)] private float _angularSpeed = 720f;
        [SerializeField, Min(0.001f)] private float _stoppingDistance = 0.2f;
        [SerializeField, Min(0.01f)] private float _navigationSampleRadius = 2f;

        [Header("Recovery")]
        [SerializeField, Min(0.01f)] private float _taskTimeout = 20f;

        [Header("Worker Trolley")]
        [SerializeField, Min(2)] private int _trolleyCapacity = 3;
        [SerializeField, Min(0.01f)] private float _trolleyFollowDistance = 1.7f;

        public EntityBehaviour ViewPrefab => _viewPrefab;
        public EntityBehaviour TrolleyViewPrefab => _trolleyViewPrefab;
        public int RequiredCompletedOrderCount => _requiredCompletedOrderCount;
        public int HirePrice => _hirePrice;
        public int DailyWage => _dailyWage;
        public float MovementSpeed => _movementSpeed;
        public float Acceleration => _acceleration;
        public float AngularSpeed => _angularSpeed;
        public float StoppingDistance => _stoppingDistance;
        public float NavigationSampleRadius => _navigationSampleRadius;
        public float TaskTimeout => _taskTimeout;
        public int TrolleyCapacity => _trolleyCapacity;
        public float TrolleyFollowDistance => _trolleyFollowDistance;

        public void Validate()
        {
            const string owner = nameof(WarehouseWorkerConfig);
            ConfigValidation.RequireReference(_viewPrefab, owner, nameof(ViewPrefab));
            ConfigValidation.RequireReference(
                _trolleyViewPrefab,
                owner,
                nameof(TrolleyViewPrefab));
            ConfigValidation.RequirePositive(
                _requiredCompletedOrderCount,
                owner,
                nameof(RequiredCompletedOrderCount));
            ConfigValidation.RequirePositive(_hirePrice, owner, nameof(HirePrice));
            ConfigValidation.RequirePositive(_dailyWage, owner, nameof(DailyWage));
            ConfigValidation.RequirePositive(_movementSpeed, owner, nameof(MovementSpeed));
            ConfigValidation.RequirePositive(_acceleration, owner, nameof(Acceleration));
            ConfigValidation.RequirePositive(_angularSpeed, owner, nameof(AngularSpeed));
            ConfigValidation.RequirePositive(
                _stoppingDistance,
                owner,
                nameof(StoppingDistance));
            ConfigValidation.RequirePositive(
                _navigationSampleRadius,
                owner,
                nameof(NavigationSampleRadius));
            ConfigValidation.RequirePositive(_taskTimeout, owner, nameof(TaskTimeout));
            ConfigValidation.RequirePositive(
                _trolleyCapacity,
                owner,
                nameof(TrolleyCapacity));
            if (_trolleyCapacity < 2)
            {
                throw new System.InvalidOperationException(
                    $"{owner}.{nameof(TrolleyCapacity)} must be at least 2.");
            }
            ConfigValidation.RequirePositive(
                _trolleyFollowDistance,
                owner,
                nameof(TrolleyFollowDistance));
        }
    }
}
