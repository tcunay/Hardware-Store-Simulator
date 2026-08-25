using System;
using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "ForkliftConfig",
        menuName = "Hardware Store/Gameplay/Forklift Config")]
    public sealed class ForkliftConfig : ScriptableObject, IValidatableConfig
    {
        [Header("View")]
        [SerializeField] private EntityBehaviour _viewPrefab;

        [Header("Driving")]
        [SerializeField, Min(0.01f)] private float _forwardSpeed = 4.5f;
        [SerializeField, Min(0.01f)] private float _reverseSpeed = 3f;
        [SerializeField, Min(0.01f)] private float _steeringSpeed = 70f;

        [Header("Lift")]
        [SerializeField, Min(0.01f)] private float _liftSpeed = 1.25f;
        [SerializeField, Min(0f)] private float _minForkHeight = 0.15f;
        [SerializeField, Min(0.01f)] private float _maxForkHeight = 2.4f;
        [SerializeField, Min(0f)] private float _initialForkHeight = 0.15f;
        [SerializeField, Min(0.01f)] private float _transferDistance = 1.25f;
        [SerializeField, Min(0.01f)] private float _transferHeightTolerance = 0.35f;
        [SerializeField, Range(0.01f, 90f)]
        private float _transferMaxAlignmentAngle = 25f;

        public EntityBehaviour ViewPrefab => _viewPrefab;
        public float ForwardSpeed => _forwardSpeed;
        public float ReverseSpeed => _reverseSpeed;
        public float SteeringSpeed => _steeringSpeed;
        public float LiftSpeed => _liftSpeed;
        public float MinForkHeight => _minForkHeight;
        public float MaxForkHeight => _maxForkHeight;
        public float InitialForkHeight => _initialForkHeight;
        public float TransferDistance => _transferDistance;
        public float TransferHeightTolerance => _transferHeightTolerance;
        public float TransferMaxAlignmentAngle => _transferMaxAlignmentAngle;

        public void Configure(EntityBehaviour viewPrefab, float forwardSpeed,
            float reverseSpeed, float steeringSpeed, float liftSpeed,
            float minForkHeight, float maxForkHeight, float initialForkHeight,
            float transferDistance, float transferHeightTolerance,
            float transferMaxAlignmentAngle)
        {
            _viewPrefab = viewPrefab;
            _forwardSpeed = forwardSpeed;
            _reverseSpeed = reverseSpeed;
            _steeringSpeed = steeringSpeed;
            _liftSpeed = liftSpeed;
            _minForkHeight = minForkHeight;
            _maxForkHeight = maxForkHeight;
            _initialForkHeight = initialForkHeight;
            _transferDistance = transferDistance;
            _transferHeightTolerance = transferHeightTolerance;
            _transferMaxAlignmentAngle = transferMaxAlignmentAngle;
            Validate();
        }

        public void Validate()
        {
            const string owner = nameof(ForkliftConfig);
            ConfigValidation.RequireReference(_viewPrefab, owner, nameof(ViewPrefab));
            ConfigValidation.RequirePositive(_forwardSpeed, owner, nameof(ForwardSpeed));
            ConfigValidation.RequirePositive(_reverseSpeed, owner, nameof(ReverseSpeed));
            ConfigValidation.RequirePositive(_steeringSpeed, owner, nameof(SteeringSpeed));
            ConfigValidation.RequirePositive(_liftSpeed, owner, nameof(LiftSpeed));
            ConfigValidation.RequireNonNegative(
                _minForkHeight,
                owner,
                nameof(MinForkHeight));
            ConfigValidation.RequirePositive(_maxForkHeight, owner, nameof(MaxForkHeight));
            if (_maxForkHeight <= _minForkHeight)
            {
                throw new InvalidOperationException(
                    $"{owner}.{nameof(MaxForkHeight)} must be greater than " +
                    $"{nameof(MinForkHeight)}.");
            }
            ConfigValidation.RequireInRange(
                _initialForkHeight,
                _minForkHeight,
                _maxForkHeight,
                owner,
                nameof(InitialForkHeight));
            ConfigValidation.RequirePositive(
                _transferDistance,
                owner,
                nameof(TransferDistance));
            ConfigValidation.RequirePositive(
                _transferHeightTolerance,
                owner,
                nameof(TransferHeightTolerance));
            ConfigValidation.RequireInRange(
                _transferMaxAlignmentAngle,
                0.01f,
                90f,
                owner,
                nameof(TransferMaxAlignmentAngle));
        }
    }
}
