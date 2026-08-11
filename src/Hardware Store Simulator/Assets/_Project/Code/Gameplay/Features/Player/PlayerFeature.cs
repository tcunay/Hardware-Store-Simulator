using HardwareStore.Gameplay.Features.Player.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Player
{
    public sealed class PlayerFeature : Feature
    {
        public PlayerFeature(ISystemFactory systems)
        {
            Add(systems.Create<InitializeCursorSystem>());
            Add(systems.Create<ValidatePlayerModalStateSystem>());
            Add(systems.Create<ToggleCursorSystem>());
            Add(systems.Create<ApplyLookInputSystem>());
        }
    }
}
