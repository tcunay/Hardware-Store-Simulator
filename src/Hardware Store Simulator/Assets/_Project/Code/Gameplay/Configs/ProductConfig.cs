using HardwareStore.Gameplay.Components;
using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "ProductConfig", menuName = "Hardware Store/Gameplay/Product Config")]
    public sealed class ProductConfig : ScriptableObject
    {
        [Header("View")]
        [SerializeField] private EntityBehaviour _viewPrefab;

        [Header("Product")]
        [SerializeField] private ProductTypeId _productType = ProductTypeId.CementBag;
        [SerializeField, Min(0)] private int _unitPrice = 350;
        [SerializeField, Min(0f)] private float _mass = 25f;

        [Header("Handling")]
        [SerializeField] private Vector3 _heldRotationEuler = new(8f, 0f, 0f);
        [SerializeField, Min(0f)] private float _dropForwardDistance = 1.15f;

        [Header("Physics")]
        [SerializeField] private RigidbodyInterpolation _worldInterpolation = RigidbodyInterpolation.Interpolate;
        [SerializeField] private CollisionDetectionMode _worldCollisionDetection =
            CollisionDetectionMode.ContinuousSpeculative;

        public EntityBehaviour ViewPrefab => _viewPrefab;
        public ProductTypeId ProductType => _productType;
        public int UnitPrice => _unitPrice;
        public float Mass => _mass;
        public Quaternion HeldRotationOffset => Quaternion.Euler(_heldRotationEuler);
        public float DropForwardDistance => _dropForwardDistance;
        public RigidbodyInterpolation WorldInterpolation => _worldInterpolation;
        public CollisionDetectionMode WorldCollisionDetection => _worldCollisionDetection;
    }
}
