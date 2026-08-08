using HardwareStore.Gameplay.Components;
using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "OrderConfig", menuName = "Hardware Store/Gameplay/Order Config")]
    public sealed class OrderConfig : ScriptableObject
    {
        [SerializeField] private ProductTypeId _requiredProductType = ProductTypeId.CementBag;
        [SerializeField, Min(1)] private int _requiredProductCount = 5;
        [SerializeField, Min(0)] private int _reward = 1750;
        [SerializeField, Min(0)] private int _initialMoney;

        public ProductTypeId RequiredProductType => _requiredProductType;
        public int RequiredProductCount => _requiredProductCount;
        public int Reward => _reward;
        public int InitialMoney => _initialMoney;
    }
}
