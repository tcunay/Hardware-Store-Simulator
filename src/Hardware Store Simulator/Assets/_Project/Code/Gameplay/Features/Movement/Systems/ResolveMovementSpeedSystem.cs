using Entitas;

namespace HardwareStore.Gameplay.Features.Movement.Systems
{
    public sealed class ResolveMovementSpeedSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;

        public ResolveMovementSpeedSystem(GameContext gameContext, InputContext inputContext)
        {
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.WalkSpeed,
                GameMatcher.SprintSpeed,
                GameMatcher.CarryingSpeed,
                GameMatcher.MovementSpeed));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(InputMatcher.InputState));
        }

        public void Execute()
        {
            foreach (InputEntity input in _inputs)
            foreach (GameEntity player in _players)
            {
                float speed = player.isHandsOccupied
                    ? player.CarryingSpeed
                    : input.isSprintHeld
                        ? player.SprintSpeed
                        : player.WalkSpeed;

                player.ReplaceMovementSpeed(speed);
            }
        }
    }
}
