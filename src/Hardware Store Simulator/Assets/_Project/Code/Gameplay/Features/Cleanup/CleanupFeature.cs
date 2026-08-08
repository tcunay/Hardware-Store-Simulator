using HardwareStore.Gameplay.Features.Cleanup.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Cleanup
{
    public sealed class CleanupFeature : Feature
    {
        public CleanupFeature(ISystemFactory systems)
        {
            Add(systems.Create<CleanupDestructedViewsSystem>());
            Add(systems.Create<CleanupDestructedEntitiesSystem>());
            Add(systems.Create<DestroyProcessedEventsSystem>());
            Add(systems.Create<CleanupInputRequestsSystem>());
            Add(systems.Create<ReleaseEntityViewsSystem>());
        }
    }
}
