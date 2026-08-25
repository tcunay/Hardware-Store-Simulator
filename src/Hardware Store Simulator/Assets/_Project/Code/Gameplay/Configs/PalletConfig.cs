using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "PalletConfig",
        menuName = "Hardware Store/Gameplay/Pallet Config")]
    public sealed class PalletConfig : ScriptableObject, IValidatableConfig
    {
        [Header("View")]
        [SerializeField] private EntityBehaviour _viewPrefab;

        public EntityBehaviour ViewPrefab => _viewPrefab;

        public void Configure(EntityBehaviour viewPrefab)
        {
            _viewPrefab = viewPrefab;
            Validate();
        }

        public void Validate()
        {
            ConfigValidation.RequireReference(
                _viewPrefab,
                nameof(PalletConfig),
                nameof(ViewPrefab));
        }
    }
}
