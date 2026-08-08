using HardwareStore.Gameplay.Features.Products.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Products
{
    public sealed class ProductsFeature : Feature
    {
        public ProductsFeature(ISystemFactory systems) =>
            Add(systems.Create<ApplyProductPhysicsConfigSystem>());
    }
}
