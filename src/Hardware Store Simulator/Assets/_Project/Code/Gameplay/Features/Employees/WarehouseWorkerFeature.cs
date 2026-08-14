using HardwareStore.Gameplay.Features.Employees.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Employees
{
    public sealed class WarehouseWorkerFeature : Feature
    {
        public WarehouseWorkerFeature(ISystemFactory systems)
        {
            Add(systems.Create<ConfigureWarehouseWorkerNavigationSystem>());
            Add(systems.Create<CleanupBlockedWarehouseTaskSystem>());
            Add(systems.Create<GenerateCustomerLoadingTaskSystem>());
            Add(systems.Create<GenerateInboundStorageTaskSystem>());
            Add(systems.Create<AssignWarehouseTaskSystem>());
            Add(systems.Create<TickWarehouseTaskTimeoutSystem>());
            Add(systems.Create<ExecuteInboundStorageTaskSystem>());
            Add(systems.Create<ExecuteCustomerLoadingTaskSystem>());
            Add(systems.Create<DetectOrphanedWarehouseTaskSystem>());
            Add(systems.Create<RecoverBlockedInboundTaskSystem>());
            Add(systems.Create<RecoverBlockedCustomerLoadingTaskSystem>());
            Add(systems.Create<ValidateWarehouseWorkerStateSystem>());
        }
    }
}
