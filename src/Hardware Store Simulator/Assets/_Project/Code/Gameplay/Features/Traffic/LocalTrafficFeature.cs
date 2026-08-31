using HardwareStore.Gameplay.Features.Traffic.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Traffic
{
    public sealed class LocalTrafficFeature : Feature
    {
        public LocalTrafficFeature(ISystemFactory systems)
        {
            Add(systems.Create<SyncTrafficIntentSystem>());
            Add(systems.Create<ResolveLocalTrafficSystem>());
            Add(systems.Create<ValidateLocalTrafficStateSystem>());
        }
    }
}
