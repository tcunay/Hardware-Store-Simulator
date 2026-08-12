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
            Add(systems.Create<GenerateInboundStorageTaskSystem>());
            Add(systems.Create<ExecuteWarehouseWorkerTaskSystem>());
            Add(systems.Create<DetectOrphanedWarehouseTaskSystem>());
            Add(systems.Create<RecoverBlockedWarehouseTaskSystem>());
            Add(systems.Create<ValidateWarehouseWorkerStateSystem>());
        }
    }
}
