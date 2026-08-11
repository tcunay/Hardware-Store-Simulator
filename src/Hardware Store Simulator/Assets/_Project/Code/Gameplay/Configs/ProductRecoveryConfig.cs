using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(
        fileName = "ProductRecoveryConfig",
        menuName = "Hardware Store/Gameplay/Product Recovery Config")]
    public sealed class ProductRecoveryConfig : ScriptableObject, IValidatableConfig
    {
        [SerializeField, Tooltip("Loose products below this world Y are returned to their reserved slot.")]
        private float _minimumWorldY = -10f;

        public float MinimumWorldY => _minimumWorldY;

        public void Validate() => ConfigValidation.RequireNegative(
            _minimumWorldY,
            nameof(ProductRecoveryConfig),
            nameof(MinimumWorldY));
    }
}
