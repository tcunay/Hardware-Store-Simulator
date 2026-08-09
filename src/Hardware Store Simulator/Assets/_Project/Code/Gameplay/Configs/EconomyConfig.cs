using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "Hardware Store/Gameplay/Economy Config")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [SerializeField, Min(0)] private int _initialMoney = 1000;

        public int InitialMoney => _initialMoney;
    }
}
