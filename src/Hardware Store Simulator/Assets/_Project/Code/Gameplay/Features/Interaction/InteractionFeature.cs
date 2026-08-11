using HardwareStore.Gameplay.Features.Interaction.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Interaction
{
    public sealed class InteractionFeature : Feature
    {
        public InteractionFeature(ISystemFactory systems)
        {
            Add(systems.Create<DetectFocusedInteractableSystem>());
            Add(systems.Create<UpdateFocusHighlightSystem>());
            Add(systems.Create<ClassifyFocusedInteractionSystem>());
            Add(systems.Create<InteractionPromptFeature>());
            Add(systems.Create<EmitInteractionRequestSystem>());
        }
    }
}
