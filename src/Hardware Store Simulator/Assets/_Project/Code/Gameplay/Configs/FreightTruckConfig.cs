using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "FreightTruckConfig",
        menuName = "Hardware Store/Gameplay/Freight Truck Config")]
    public sealed class FreightTruckConfig : ScriptableObject, IValidatableConfig
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
                nameof(FreightTruckConfig),
                nameof(ViewPrefab));
        }
    }
}
