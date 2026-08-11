using Entitas;
using HardwareStore.Gameplay.Common.Physics;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class DetectFocusedInteractableSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IInteractionPhysicsService _physics;
        private readonly IGroup<GameEntity> _players;

        public DetectFocusedInteractableSystem(GameContext gameContext, IInteractionPhysicsService physics)
        {
            _gameContext = gameContext;
            _physics = physics;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.Camera,
                GameMatcher.InteractionDistance,
                GameMatcher.AimAssistRadius)
                .NoneOf(GameMatcher.ModalOpen, GameMatcher.PushingTrolley));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                if (_physics.TryGetFocusedEntity(player.Camera,
                        player.InteractionDistance, player.AimAssistRadius, out int entityId))
                {
                    GameEntity target = _gameContext.GetEntityWithEntityId(entityId);
                    if (target != null && target.isInteractable && !target.isDestructed)
                    {
                        player.ReplaceFocusedEntityId(entityId);
                        continue;
                    }
                }

                if (player.hasFocusedEntityId)
                    player.RemoveFocusedEntityId();
            }
        }
    }
}
