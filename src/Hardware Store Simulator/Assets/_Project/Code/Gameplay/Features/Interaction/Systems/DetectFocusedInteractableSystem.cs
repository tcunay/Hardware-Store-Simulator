using Entitas;
using HardwareStore.Gameplay.Common.Physics;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class DetectFocusedInteractableSystem : IExecuteSystem
    {
        private readonly IInteractionPhysicsService _physics;
        private readonly IGroup<GameEntity> _players;

        public DetectFocusedInteractableSystem(GameContext gameContext, IInteractionPhysicsService physics)
        {
            _physics = physics;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.Camera,
                GameMatcher.InteractionDistance,
                GameMatcher.AimAssistRadius));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                if (_physics.TryGetFocusedEntity(player.Camera,
                        player.InteractionDistance, player.AimAssistRadius, out int entityId))
                {
                    player.ReplaceFocusedEntityId(entityId);
                }
                else if (player.hasFocusedEntityId)
                {
                    player.RemoveFocusedEntityId();
                }
            }
        }
    }
}
