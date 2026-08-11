using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "PlatformTrolleyConfig",
        menuName = "Hardware Store/Gameplay/Platform Trolley Config")]
    public sealed class PlatformTrolleyConfig : ScriptableObject, IValidatableConfig
    {
        [Header("View")]
        [SerializeField] private EntityBehaviour _viewPrefab;

        [Header("Upgrade")]
        [SerializeField, Min(1)] private int _purchasePrice = 200;
        [SerializeField, Min(1)] private int _requiredCompletedOrderCount = 2;

        [Header("Cargo")]
        [SerializeField, Min(1)] private int _capacity = 3;

        [Header("Movement")]
        [SerializeField, Min(0.01f)] private float _movementSpeed = 3.8f;
        [SerializeField, Min(0.01f)] private float _followDistance = 1.7f;

        public EntityBehaviour ViewPrefab => _viewPrefab;
        public int PurchasePrice => _purchasePrice;
        public int RequiredCompletedOrderCount => _requiredCompletedOrderCount;
        public int Capacity => _capacity;
        public float MovementSpeed => _movementSpeed;
        public float FollowDistance => _followDistance;

        public void Configure(EntityBehaviour viewPrefab, int purchasePrice,
            int requiredCompletedOrderCount, int capacity, float movementSpeed,
            float followDistance)
        {
            _viewPrefab = viewPrefab;
            _purchasePrice = purchasePrice;
            _requiredCompletedOrderCount = requiredCompletedOrderCount;
            _capacity = capacity;
            _movementSpeed = movementSpeed;
            _followDistance = followDistance;
            Validate();
        }

        public void Validate()
        {
            const string owner = nameof(PlatformTrolleyConfig);
            ConfigValidation.RequireReference(_viewPrefab, owner, nameof(ViewPrefab));
            ConfigValidation.RequirePositive(_purchasePrice, owner, nameof(PurchasePrice));
            ConfigValidation.RequirePositive(
                _requiredCompletedOrderCount,
                owner,
                nameof(RequiredCompletedOrderCount));
            ConfigValidation.RequirePositive(_capacity, owner, nameof(Capacity));
            ConfigValidation.RequirePositive(
                _movementSpeed,
                owner,
                nameof(MovementSpeed));
            ConfigValidation.RequirePositive(
                _followDistance,
                owner,
                nameof(FollowDistance));
        }
    }
}
