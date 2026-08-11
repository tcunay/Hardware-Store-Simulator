using HardwareStore.Gameplay.Components;
using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "ProductConfig", menuName = "Hardware Store/Gameplay/Product Config")]
    public sealed class ProductConfig : ScriptableObject, IValidatableConfig
    {
        [Header("View")]
        [SerializeField] private EntityBehaviour _viewPrefab;

        [Header("Product")]
        [SerializeField] private ProductTypeId _productType = ProductTypeId.CementBag;
        [SerializeField, Min(0), Tooltip("Retail sale price per product unit.")]
        private int _unitPrice = 350;
        [SerializeField, Min(0f)] private float _mass = 25f;

        [Header("Handling")]
        [SerializeField, Min(0f)] private float _carryMovementSpeed = 3.2f;
        [SerializeField] private Vector3 _heldRotationEuler = new(8f, 0f, 0f);
        [SerializeField, Min(0.01f)] private float _dropForwardDistance = 1.15f;
        [SerializeField, Min(0.01f), Tooltip("Conservative radius enclosing the solid product collider.")]
        private float _productDropCollisionRadius = 0.35f;

        [Header("Physics")]
        [SerializeField] private RigidbodyInterpolation _worldInterpolation = RigidbodyInterpolation.Interpolate;
        [SerializeField] private CollisionDetectionMode _worldCollisionDetection =
            CollisionDetectionMode.ContinuousSpeculative;

        public EntityBehaviour ViewPrefab => _viewPrefab;
        public ProductTypeId ProductType => _productType;
        public int UnitPrice => _unitPrice;
        public float Mass => _mass;
        public float CarryMovementSpeed => _carryMovementSpeed;
        public Quaternion HeldRotationOffset => Quaternion.Euler(_heldRotationEuler);
        public float DropForwardDistance => _dropForwardDistance;
        public float ProductDropCollisionRadius => _productDropCollisionRadius;
        public RigidbodyInterpolation WorldInterpolation => _worldInterpolation;
        public CollisionDetectionMode WorldCollisionDetection => _worldCollisionDetection;

        public void Validate()
        {
            const string owner = nameof(ProductConfig);
            ConfigValidation.RequireReference(_viewPrefab, owner, nameof(ViewPrefab));
            ConfigValidation.RequireDefined(_productType, owner, nameof(ProductType));
            ConfigValidation.RequireNonNegative(_unitPrice, owner, nameof(UnitPrice));
            ConfigValidation.RequirePositive(_mass, owner, nameof(Mass));
            ConfigValidation.RequirePositive(
                _carryMovementSpeed,
                owner,
                nameof(CarryMovementSpeed));
            ConfigValidation.RequireFinite(
                _heldRotationEuler,
                owner,
                nameof(HeldRotationOffset));
            ConfigValidation.RequirePositive(
                _dropForwardDistance,
                owner,
                nameof(DropForwardDistance));
            ConfigValidation.RequirePositive(
                _productDropCollisionRadius,
                owner,
                nameof(ProductDropCollisionRadius));
            if (_productDropCollisionRadius > _dropForwardDistance)
            {
                throw new System.InvalidOperationException(
                    $"{owner}.ProductDropCollisionRadius must not exceed DropForwardDistance.");
            }
            ConfigValidation.RequireDefined(
                _worldInterpolation,
                owner,
                nameof(WorldInterpolation));
            ConfigValidation.RequireDefined(
                _worldCollisionDetection,
                owner,
                nameof(WorldCollisionDetection));
        }
    }
}
