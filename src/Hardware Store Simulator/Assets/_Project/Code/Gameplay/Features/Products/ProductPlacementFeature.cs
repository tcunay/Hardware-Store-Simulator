using HardwareStore.Gameplay.Features.Products.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Products
{
    public sealed class ProductPlacementFeature : Feature
    {
        public ProductPlacementFeature(ISystemFactory systems)
        {
            Add(systems.Create<ApplyProductMassSystem>());
            Add(systems.Create<ApplyInboundProductPlacementSystem>());
            Add(systems.Create<ApplyStockedProductPlacementSystem>());
            Add(systems.Create<ApplyCarriedProductPlacementSystem>());
            Add(systems.Create<ApplyLooseProductPlacementSystem>());
            Add(systems.Create<ApplyLoadedProductPlacementSystem>());
            Add(systems.Create<ApplyTrolleyProductPlacementSystem>());
            Add(systems.Create<ValidateProductPlacementSystem>());
        }
    }
}
