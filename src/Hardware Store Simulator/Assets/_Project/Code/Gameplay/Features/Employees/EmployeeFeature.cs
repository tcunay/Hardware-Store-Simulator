using HardwareStore.Gameplay.Features.Employees.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Employees
{
    public sealed class EmployeeFeature : Feature
    {
        public EmployeeFeature(ISystemFactory systems)
        {
            Add(systems.Create<UnlockWarehouseWorkerHiringSystem>());
            Add(systems.Create<SyncWarehouseWorkerShiftSystem>());
            Add(systems.Create<PayWarehouseWorkerShiftSystem>());
            Add(systems.Create<HireWarehouseWorkerSystem>());
            Add(systems.Create<WarehouseWorkerFeature>());
        }
    }
}
