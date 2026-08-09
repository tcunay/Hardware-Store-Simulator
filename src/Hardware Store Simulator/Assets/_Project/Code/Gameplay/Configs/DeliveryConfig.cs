using HardwareStore.Gameplay.Components;
using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "DeliveryConfig", menuName = "Hardware Store/Gameplay/Delivery Config")]
    public sealed class DeliveryConfig : ScriptableObject
    {
        [Header("View")]
        [SerializeField] private EntityBehaviour _viewPrefab;

        [Header("Cargo")]
        [SerializeField] private ProductTypeId _productType = ProductTypeId.CementBag;
        [SerializeField, Min(1)] private int _productCount = 3;
        [SerializeField, Min(0)] private int _purchaseUnitPrice = 200;

        public EntityBehaviour ViewPrefab => _viewPrefab;
        public ProductTypeId ProductType => _productType;
        public int ProductCount => _productCount;
        public int PurchaseUnitPrice => _purchaseUnitPrice;
        public int TotalCost => ProductCount * PurchaseUnitPrice;
    }
}
