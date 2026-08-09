using HardwareStore.Gameplay.Components;
using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "OrderConfig", menuName = "Hardware Store/Gameplay/Order Config")]
    public sealed class OrderConfig : ScriptableObject
    {
        [SerializeField] private ProductTypeId _requiredProductType = ProductTypeId.CementBag;
        [SerializeField, Min(1)] private int _requiredProductCount = 2;
        [SerializeField, Min(0)] private int _reward = 700;

        public ProductTypeId RequiredProductType => _requiredProductType;
        public int RequiredProductCount => _requiredProductCount;
        public int Reward => _reward;
    }
}
