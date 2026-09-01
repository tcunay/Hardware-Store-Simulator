using HardwareStore.Gameplay.Features.Trolley.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Trolley
{
    public sealed class TrolleyFeature : Feature
    {
        public TrolleyFeature(ISystemFactory systems)
        {
            Add(systems.Create<DetachPushedTrolleySystem>());
            Add(systems.Create<RegisterCompletedOrderForProgressionSystem>());
            Add(systems.Create<UnlockPlatformTrolleyUpgradeSystem>());
            Add(systems.Create<PurchasePlatformTrolleySystem>());
            Add(systems.Create<StartPushingTrolleySystem>());
            Add(systems.Create<SyncTrolleyCollisionProfileSystem>());
            Add(systems.Create<LoadHeldProductOnTrolleySystem>());
            Add(systems.Create<RefreshTrolleyOccupiedSlotCountSystem>());
            Add(systems.Create<SyncTrolleyNavigationObstacleSystem>());
            Add(systems.Create<ValidatePlayerHandlingStateSystem>());
            Add(systems.Create<ValidatePlatformTrolleyStateSystem>());
        }
    }
}
