using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "LocalTrafficConfig",
        menuName = "Hardware Store/Gameplay/Local Traffic Config")]
    public sealed class LocalTrafficConfig : ScriptableObject, IValidatableConfig
    {
        [Header("Prediction")]
        [SerializeField, Min(0.1f)] private float _predictionHorizon = 1.75f;
        [SerializeField, Min(4)] private int _predictionStepCount = 32;
        [SerializeField, Min(0f)] private float _safetyClearance = 0.08f;
        [SerializeField, Min(0f)] private float _releaseClearance = 0.16f;
        [SerializeField, Min(0f)] private float _arrivalPriorityTolerance = 0.2f;
        [SerializeField, Min(0f)] private float _stationarySpeed = 0.05f;

        [Header("Vehicle Response")]
        [SerializeField, Min(0.01f)] private float _vehicleAcceleration = 4.5f;
        [SerializeField, Min(0.01f)] private float _vehicleBraking = 8f;

        public float PredictionHorizon => _predictionHorizon;
        public int PredictionStepCount => _predictionStepCount;
        public float SafetyClearance => _safetyClearance;
        public float ReleaseClearance => _releaseClearance;
        public float ArrivalPriorityTolerance => _arrivalPriorityTolerance;
        public float StationarySpeed => _stationarySpeed;
        public float VehicleAcceleration => _vehicleAcceleration;
        public float VehicleBraking => _vehicleBraking;

        public void Validate()
        {
            const string owner = nameof(LocalTrafficConfig);
            ConfigValidation.RequirePositive(
                _predictionHorizon, owner, nameof(PredictionHorizon));
            ConfigValidation.RequirePositive(
                _predictionStepCount, owner, nameof(PredictionStepCount));
            if (_predictionStepCount < 4)
            {
                throw new System.InvalidOperationException(
                    $"{owner}.{nameof(PredictionStepCount)} must be at least 4.");
            }
            ConfigValidation.RequireNonNegative(
                _safetyClearance, owner, nameof(SafetyClearance));
            ConfigValidation.RequireNonNegative(
                _releaseClearance, owner, nameof(ReleaseClearance));
            if (_releaseClearance < _safetyClearance)
            {
                throw new System.InvalidOperationException(
                    $"{owner}.{nameof(ReleaseClearance)} must be greater than or equal to " +
                    $"{nameof(SafetyClearance)}.");
            }
            ConfigValidation.RequireNonNegative(
                _arrivalPriorityTolerance,
                owner,
                nameof(ArrivalPriorityTolerance));
            ConfigValidation.RequireNonNegative(
                _stationarySpeed, owner, nameof(StationarySpeed));
            ConfigValidation.RequirePositive(
                _vehicleAcceleration, owner, nameof(VehicleAcceleration));
            ConfigValidation.RequirePositive(
                _vehicleBraking, owner, nameof(VehicleBraking));
        }
    }
}
