using HardwareStore.Gameplay.Features.Interaction.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Interaction
{
    public sealed class InteractionPromptFeature : Feature
    {
        public InteractionPromptFeature(ISystemFactory systems)
        {
            Add(systems.Create<ClearInteractionPromptSystem>());
            Add(systems.Create<ResolveProcurementTerminalPromptSystem>());
            Add(systems.Create<ResolveEmptyHandsStoragePromptSystem>());
            Add(systems.Create<ResolveHeldProductStoragePromptSystem>());
            Add(systems.Create<ResolveOrderCounterPromptSystem>());
            Add(systems.Create<ResolveProductPromptSystem>());
            Add(systems.Create<ResolveLoadingZonePromptSystem>());
            Add(systems.Create<ResolveTrolleyUpgradeTerminalPromptSystem>());
            Add(systems.Create<ResolvePlatformTrolleyPromptSystem>());
            Add(systems.Create<ResolveStoreControlTerminalPromptSystem>());
        }
    }
}
