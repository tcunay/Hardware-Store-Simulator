using HardwareStore.Gameplay.Features.Carrying.Systems;
using HardwareStore.Gameplay.Features.Products.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Carrying
{
    public sealed class LateCarryingFeature : Feature
    {
        public LateCarryingFeature(ISystemFactory systems)
        {
            Add(systems.Create<FollowHeldProductSystem>());
            Add(systems.Create<FollowWorkerCarriedProductSystem>());
            Add(systems.Create<SyncLooseProductPoseSystem>());
        }
    }
}
