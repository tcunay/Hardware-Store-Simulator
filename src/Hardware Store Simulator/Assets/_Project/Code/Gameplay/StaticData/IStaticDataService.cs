using System.Collections.Generic;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;

namespace HardwareStore.Gameplay.StaticData
{
    public interface IStaticDataService
    {
        PlayerConfig Player { get; }
        InteractionConfig Interaction { get; }
        EconomyConfig Economy { get; }
        CustomerConfig Customer { get; }
        CustomerVehicleConfig CustomerVehicle { get; }
        IReadOnlyList<ProductTypeId> ProductTypes { get; }

        void LoadAll();
        ProductConfig GetProduct(ProductTypeId productType);
        DeliveryConfig GetDelivery(ProductTypeId productType);
        OrderConfig GetOrder(ProductTypeId productType);
    }
}
