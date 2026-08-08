using HardwareStore.Gameplay.Features.Orders.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Orders
{
    public sealed class OrdersFeature : Feature
    {
        public OrdersFeature(ISystemFactory systems) => Add(systems.Create<AcceptOrderSystem>());
    }
}
