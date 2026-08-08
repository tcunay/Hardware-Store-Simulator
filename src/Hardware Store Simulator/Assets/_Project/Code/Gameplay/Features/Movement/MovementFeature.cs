using HardwareStore.Gameplay.Features.Movement.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Movement
{
    public sealed class MovementFeature : Feature
    {
        public MovementFeature(ISystemFactory systems)
        {
            Add(systems.Create<SetMoveDirectionFromInputSystem>());
            Add(systems.Create<ResolveMovementSpeedSystem>());
            Add(systems.Create<MoveCharacterControllerSystem>());
        }
    }
}
