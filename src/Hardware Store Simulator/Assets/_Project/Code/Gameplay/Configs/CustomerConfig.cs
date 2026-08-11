using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "CustomerConfig",
        menuName = "Hardware Store/Gameplay/Customer Config")]
    public sealed class CustomerConfig : ScriptableObject, IValidatableConfig
    {
        [Header("View")]
        [SerializeField] private EntityBehaviour _viewPrefab;

        [Header("Movement")]
        [SerializeField, Min(0.01f)] private float _movementSpeed = 2.25f;
        [SerializeField, Min(0.01f)] private float _rotationSpeed = 360f;
        [SerializeField, Min(0.001f)] private float _waypointTolerance = 0.05f;

        public EntityBehaviour ViewPrefab => _viewPrefab;
        public float MovementSpeed => _movementSpeed;
        public float RotationSpeed => _rotationSpeed;
        public float WaypointTolerance => _waypointTolerance;

        public void Configure(EntityBehaviour viewPrefab, float movementSpeed,
            float rotationSpeed, float waypointTolerance)
        {
            _viewPrefab = viewPrefab;
            _movementSpeed = movementSpeed;
            _rotationSpeed = rotationSpeed;
            _waypointTolerance = waypointTolerance;

            Validate();
        }

        public void Validate()
        {
            const string owner = nameof(CustomerConfig);
            ConfigValidation.RequireReference(_viewPrefab, owner, nameof(ViewPrefab));
            ConfigValidation.RequirePositive(_movementSpeed, owner, nameof(MovementSpeed));
            ConfigValidation.RequirePositive(_rotationSpeed, owner, nameof(RotationSpeed));
            ConfigValidation.RequirePositive(
                _waypointTolerance,
                owner,
                nameof(WaypointTolerance));
        }
    }
}
