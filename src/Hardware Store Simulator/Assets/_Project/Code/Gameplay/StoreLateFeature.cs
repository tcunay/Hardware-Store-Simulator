using HardwareStore.Gameplay.Features.Carrying;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay
{
    public sealed class StoreLateFeature : Feature
    {
        public StoreLateFeature(ISystemFactory systems) => Add(systems.Create<LateCarryingFeature>());
    }
}
