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
        [SerializeField, Min(0f)] private float _carryingSpeed = 3.2f;
        [SerializeField] private float _gravity = -24f;

        [Header("Look")]
        [SerializeField, Min(0f)] private float _mouseSensitivity = 0.1f;
        [SerializeField, Min(0f)] private float _gamepadLookSpeed = 150f;
        [SerializeField, Range(30f, 89f)] private float _maxPitch = 85f;

        public EntityBehaviour ViewPrefab => _viewPrefab;
        public float WalkSpeed => _walkSpeed;
        public float SprintSpeed => _sprintSpeed;
        public float CarryingSpeed => _carryingSpeed;
        public float Gravity => _gravity;
        public float MouseSensitivity => _mouseSensitivity;
        public float GamepadLookSpeed => _gamepadLookSpeed;
        public float MaxPitch => _maxPitch;

        public void Validate()
        {
            const string owner = nameof(PlayerConfig);
            ConfigValidation.RequireReference(_viewPrefab, owner, nameof(ViewPrefab));
            ConfigValidation.RequirePositive(_walkSpeed, owner, nameof(WalkSpeed));
            ConfigValidation.RequirePositive(_sprintSpeed, owner, nameof(SprintSpeed));
            ConfigValidation.RequirePositive(_carryingSpeed, owner, nameof(CarryingSpeed));
            if (!(_carryingSpeed < _walkSpeed && _walkSpeed < _sprintSpeed))
                throw new System.InvalidOperationException(
                    $"{owner} movement speeds must satisfy CarryingSpeed < WalkSpeed < SprintSpeed.");

            ConfigValidation.RequireNegative(_gravity, owner, nameof(Gravity));
            ConfigValidation.RequirePositive(_mouseSensitivity, owner, nameof(MouseSensitivity));
            ConfigValidation.RequirePositive(_gamepadLookSpeed, owner, nameof(GamepadLookSpeed));
            ConfigValidation.RequireInRange(_maxPitch, 30f, 89f, owner, nameof(MaxPitch));
        }
    }
}
