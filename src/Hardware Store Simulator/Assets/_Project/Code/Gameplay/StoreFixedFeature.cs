using HardwareStore.Gameplay.Features.Customers.Systems;
using HardwareStore.Gameplay.Features.Employees.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay
{
    public sealed class StoreFixedFeature : Feature
    {
        public StoreFixedFeature(ISystemFactory systems)
        {
            Add(systems.Create<DriveWarehouseWorkerPhysicsSystem>());
            Add(systems.Create<DriveCustomerVehiclePhysicsSystem>());
        }
    }
}
