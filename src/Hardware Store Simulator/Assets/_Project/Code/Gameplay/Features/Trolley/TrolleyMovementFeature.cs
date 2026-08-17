using HardwareStore.Gameplay.Features.Trolley.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Trolley
{
    public sealed class TrolleyMovementFeature : Feature
    {
        public TrolleyMovementFeature(ISystemFactory systems)
        {
            Add(systems.Create<FollowPushedTrolleySystem>());
            Add(systems.Create<FollowWorkerTrolleySystem>());
        }
    }
}
