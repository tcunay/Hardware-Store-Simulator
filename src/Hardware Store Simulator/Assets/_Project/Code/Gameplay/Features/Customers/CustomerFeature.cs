using HardwareStore.Gameplay.Features.Customers.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Customers
{
    public sealed class CustomerFeature : Feature
    {
        public CustomerFeature(ISystemFactory systems)
        {
            Add(systems.Create<TickCustomerCooldownSystem>());
            Add(systems.Create<SpawnCustomerVisitSystem>());
            Add(systems.Create<MoveCustomerVehicleRouteSystem>());
            Add(systems.Create<CompleteCustomerVehicleArrivalSystem>());
            Add(systems.Create<BeginCustomerVehicleDepartureDelaySystem>());
            Add(systems.Create<TickCustomerVehicleDepartureDelaySystem>());
            Add(systems.Create<BeginCustomerVehicleDepartureSystem>());
            Add(systems.Create<CompleteCustomerVehicleDepartureSystem>());
        }
    }
}
