using HardwareStore.Gameplay.Features.Forklift.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Forklift
{
    public sealed class ForkliftFeature : Feature
    {
        public ForkliftFeature(ISystemFactory systems)
        {
            Add(systems.Create<RecoverDestructedForkliftStateSystem>());
            Add(systems.Create<ToggleForkliftDrivingSystem>());
            Add(systems.Create<DriveForkliftSystem>());
            Add(systems.Create<AdjustForkliftLiftSystem>());
            Add(systems.Create<SyncForkliftLiftTransformSystem>());
            Add(systems.Create<TransferForkliftPalletSystem>());
            Add(systems.Create<CompleteInboundPalletStagingSystem>());
            Add(systems.Create<FollowForkliftCarriedPalletSystem>());
            Add(systems.Create<FollowPalletBayPlacementSystem>());
            Add(systems.Create<ValidateForkliftFreightStateSystem>());
        }
    }
}
