using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Hardware Store/Gameplay/Player Config")]
    public sealed class PlayerConfig : ScriptableObject, IValidatableConfig
    {
        [Header("View")]
        [SerializeField] private EntityBehaviour _viewPrefab;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float _walkSpeed = 5.2f;
        [SerializeField, Min(0f)] private float _sprintSpeed = 8f;
        [SerializeField] private float _gravity = -24f;

        [Header("Look")]
        [SerializeField, Min(0f)] private float _mouseSensitivity = 0.1f;
        [SerializeField, Min(0f)] private float _gamepadLookSpeed = 150f;
        [SerializeField, Range(30f, 89f)] private float _maxPitch = 85f;

        [Header("Third Person Vehicle Camera")]
        [SerializeField, Min(0.01f)] private float _vehicleCameraDistance = 5.2f;
        [SerializeField, Range(-89f, 89f)] private float _vehicleCameraDefaultPitch = 18f;
        [SerializeField, Range(-89f, 89f)] private float _vehicleCameraMinPitch = -10f;
        [SerializeField, Range(-89f, 89f)] private float _vehicleCameraMaxPitch = 55f;
        [SerializeField, Min(0.01f)] private float _vehicleCameraCollisionRadius = 0.22f;
        [SerializeField, Min(0f)] private float _vehicleCameraCollisionPadding = 0.08f;

        public EntityBehaviour ViewPrefab => _viewPrefab;
        public float WalkSpeed => _walkSpeed;
        public float SprintSpeed => _sprintSpeed;
        public float Gravity => _gravity;
        public float MouseSensitivity => _mouseSensitivity;
        public float GamepadLookSpeed => _gamepadLookSpeed;
        public float MaxPitch => _maxPitch;
        public float VehicleCameraDistance => _vehicleCameraDistance;
        public float VehicleCameraDefaultPitch => _vehicleCameraDefaultPitch;
        public float VehicleCameraMinPitch => _vehicleCameraMinPitch;
        public float VehicleCameraMaxPitch => _vehicleCameraMaxPitch;
        public float VehicleCameraCollisionRadius => _vehicleCameraCollisionRadius;
        public float VehicleCameraCollisionPadding => _vehicleCameraCollisionPadding;

        public void Validate()
        {
            const string owner = nameof(PlayerConfig);
            ConfigValidation.RequireReference(_viewPrefab, owner, nameof(ViewPrefab));
            ConfigValidation.RequirePositive(_walkSpeed, owner, nameof(WalkSpeed));
            ConfigValidation.RequirePositive(_sprintSpeed, owner, nameof(SprintSpeed));
            if (_walkSpeed >= _sprintSpeed)
                throw new System.InvalidOperationException(
                    $"{owner} movement speeds must satisfy WalkSpeed < SprintSpeed.");

            ConfigValidation.RequireNegative(_gravity, owner, nameof(Gravity));
            ConfigValidation.RequirePositive(_mouseSensitivity, owner, nameof(MouseSensitivity));
            ConfigValidation.RequirePositive(_gamepadLookSpeed, owner, nameof(GamepadLookSpeed));
            ConfigValidation.RequireInRange(_maxPitch, 30f, 89f, owner, nameof(MaxPitch));
            ConfigValidation.RequirePositive(
                _vehicleCameraDistance,
                owner,
                nameof(VehicleCameraDistance));
            ConfigValidation.RequireInRange(
                _vehicleCameraMinPitch,
                -89f,
                89f,
                owner,
                nameof(VehicleCameraMinPitch));
            ConfigValidation.RequireInRange(
                _vehicleCameraMaxPitch,
                -89f,
                89f,
                owner,
                nameof(VehicleCameraMaxPitch));
            if (_vehicleCameraMaxPitch <= _vehicleCameraMinPitch)
            {
                throw new System.InvalidOperationException(
                    $"{owner}.{nameof(VehicleCameraMaxPitch)} must be greater than " +
                    $"{nameof(VehicleCameraMinPitch)}.");
            }
            ConfigValidation.RequireInRange(
                _vehicleCameraDefaultPitch,
                _vehicleCameraMinPitch,
                _vehicleCameraMaxPitch,
                owner,
                nameof(VehicleCameraDefaultPitch));
            ConfigValidation.RequirePositive(
                _vehicleCameraCollisionRadius,
                owner,
                nameof(VehicleCameraCollisionRadius));
            ConfigValidation.RequireNonNegative(
                _vehicleCameraCollisionPadding,
                owner,
                nameof(VehicleCameraCollisionPadding));
        }
    }
}
