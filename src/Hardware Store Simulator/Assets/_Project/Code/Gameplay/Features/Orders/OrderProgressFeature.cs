using HardwareStore.Gameplay.Features.Orders.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Orders
{
    public sealed class OrderProgressFeature : Feature
    {
        public OrderProgressFeature(ISystemFactory systems)
        {
            Add(systems.Create<RegisterLoadedProductSystem>());
            Add(systems.Create<CompleteOrderSystem>());
            Add(systems.Create<RewardCompletedOrderSystem>());
        }
    }
}
