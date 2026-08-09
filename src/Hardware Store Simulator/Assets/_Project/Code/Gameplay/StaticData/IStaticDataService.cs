using HardwareStore.Gameplay.Configs;

namespace HardwareStore.Gameplay.StaticData
{
    public interface IStaticDataService
    {
        PlayerConfig Player { get; }
        InteractionConfig Interaction { get; }
        EconomyConfig Economy { get; }
        DeliveryConfig Delivery { get; }
        CustomerVehicleConfig CustomerVehicle { get; }
        OrderConfig Order { get; }
        ProductConfig Product { get; }

        void LoadAll();
    }
}
