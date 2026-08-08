using HardwareStore.Gameplay.Components;
using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "ProductConfig", menuName = "Hardware Store/Gameplay/Product Config")]
    public sealed class ProductConfig : ScriptableObject
    {
        [SerializeField] private ProductTypeId _productType = ProductTypeId.CementBag;
        [SerializeField, Min(0)] private int _unitPrice = 350;
        [SerializeField, Min(0f)] private float _mass = 25f;

        public ProductTypeId ProductType => _productType;
        public int UnitPrice => _unitPrice;
        public float Mass => _mass;
    }
}
