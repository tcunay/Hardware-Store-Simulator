using HardwareStore.Gameplay.Features.Delivery.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Delivery
{
    public sealed class DeliveryFeature : Feature
    {
        public DeliveryFeature(ISystemFactory systems)
        {
            Add(systems.Create<PurchaseDeliverySystem>());
            Add(systems.Create<CloseProcurementAfterPurchaseSystem>());
            Add(systems.Create<SpawnDeliveryProductsSystem>());
            Add(systems.Create<StoreInboundProductSystem>());
            Add(systems.Create<RegisterStockedProductSystem>());
            Add(systems.Create<CompleteDeliverySystem>());
        }
    }
}
