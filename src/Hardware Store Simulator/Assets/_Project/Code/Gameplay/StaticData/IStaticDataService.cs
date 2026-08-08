using HardwareStore.Gameplay.Configs;

namespace HardwareStore.Gameplay.StaticData
{
    public interface IStaticDataService
    {
        PlayerConfig Player { get; }
        InteractionConfig Interaction { get; }
        OrderConfig Order { get; }
        ProductConfig Product { get; }

        void LoadAll();
    }
}
