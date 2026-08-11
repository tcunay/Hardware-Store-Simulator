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
                if (player.isPushingTrolley)
                {
                    if (!player.isHandsOccupied || player.isCarryingProduct)
                        throw new System.InvalidOperationException(
                            $"Player {player.EntityId} has invalid trolley handling state.");

                    GameEntity trolley =
                        _gameContext.GetEntityWithTrolleyPusherEntityId(player.EntityId);
                    speed = trolley.TrolleyMovementSpeed;
                }
                else if (player.isCarryingProduct)
                {
                    if (!player.isHandsOccupied)
                        throw new System.InvalidOperationException(
                            $"Player {player.EntityId} carries a product with free hands.");
                    GameEntity product =
                        _gameContext.GetEntityWithCarrierEntityId(player.EntityId);
                    speed = product.CarryMovementSpeed;
                }
                else
                {
                    if (player.isHandsOccupied)
                        throw new System.InvalidOperationException(
                            $"Player {player.EntityId} has occupied hands without a handling role.");
                    speed = input.isSprintHeld
                        ? player.SprintSpeed
                        : player.WalkSpeed;
                }

                player.ReplaceMovementSpeed(speed);
            }
        }
    }
}
