using HardwareStore.Gameplay.Features.Products.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Products
{
    public sealed class ProductRecoveryFeature : Feature
    {
        public ProductRecoveryFeature(ISystemFactory systems) =>
            Add(systems.Create<RecoverLostLooseProductsSystem>());
    }
}
