using HardwareStore.Gameplay.Features.Input.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Input
{
    public sealed class InputFeature : Feature
    {
        public InputFeature(ISystemFactory systems)
        {
            Add(systems.Create<InitializeInputEntitySystem>());
            Add(systems.Create<EmitInputSystem>());
        }
    }
}
