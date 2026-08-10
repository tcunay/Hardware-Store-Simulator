using Entitas;

namespace HardwareStore.Gameplay.Features.Movement.Systems
{
    public sealed class ResolveMovementSpeedSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;

        public ResolveMovementSpeedSystem(GameContext gameContext, InputContext inputContext)
        {
            _gameContext = gameContext;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.EntityId,
                GameMatcher.WalkSpeed,
                GameMatcher.SprintSpeed,
                GameMatcher.MovementSpeed));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(InputMatcher.InputState));
        }

        public void Execute()
        {
            foreach (InputEntity input in _inputs)
            foreach (GameEntity player in _players)
            {
                float speed;
                if (player.isHandsOccupied)
                {
                    GameEntity product =
                        _gameContext.GetEntityWithCarrierEntityId(player.EntityId);
                    speed = product.CarryMovementSpeed;
                }
                else
                {
                    speed = input.isSprintHeld
                        ? player.SprintSpeed
                        : player.WalkSpeed;
                }

                player.ReplaceMovementSpeed(speed);
            }
        }
    }
}
