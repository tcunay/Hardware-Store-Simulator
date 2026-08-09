using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "InteractionConfig", menuName = "Hardware Store/Gameplay/Interaction Config")]
    public sealed class InteractionConfig : ScriptableObject, IValidatableConfig
    {
        [SerializeField, Min(0.5f)] private float _interactionDistance = 3.4f;
        [SerializeField, Range(0f, 0.6f)] private float _aimAssistRadius = 0.26f;

        public float Distance => _interactionDistance;
        public float AimAssistRadius => _aimAssistRadius;

        public void Validate()
        {
            const string owner = nameof(InteractionConfig);
            ConfigValidation.RequireInRange(
                _interactionDistance,
                0.5f,
                float.MaxValue,
                owner,
                nameof(Distance));
            ConfigValidation.RequireInRange(
                _aimAssistRadius,
                0f,
                0.6f,
                owner,
                nameof(AimAssistRadius));
            if (_aimAssistRadius >= _interactionDistance)
                throw new System.InvalidOperationException(
                    $"{owner}.AimAssistRadius must be less than Distance.");
        }
    }
}
