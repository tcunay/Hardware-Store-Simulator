using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "InteractionConfig", menuName = "Hardware Store/Gameplay/Interaction Config")]
    public sealed class InteractionConfig : ScriptableObject
    {
        [SerializeField, Min(0.5f)] private float _interactionDistance = 3.4f;
        [SerializeField, Range(0f, 0.6f)] private float _aimAssistRadius = 0.26f;

        public float Distance => _interactionDistance;
        public float AimAssistRadius => _aimAssistRadius;
    }
}
