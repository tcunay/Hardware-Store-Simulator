using Entitas;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Movement.Systems
{
    public sealed class SetMoveDirectionFromInputSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;

        public SetMoveDirectionFromInputSystem(GameContext gameContext, InputContext inputContext)
        {
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.Transform,
                GameMatcher.MoveDirection));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(
                InputMatcher.InputState,
                InputMatcher.MoveInput));
        }

        public void Execute()
        {
            foreach (InputEntity input in _inputs)
            foreach (GameEntity player in _players)
            {
                Vector2 move = player.isModalOpen || player.isDrivingForklift
                    ? Vector2.zero
                    : Vector2.ClampMagnitude(input.MoveInput, 1f);
                Transform playerTransform = player.Transform;
                Vector3 direction = playerTransform.right * move.x + playerTransform.forward * move.y;
                player.ReplaceMoveDirection(direction);
            }
        }
    }
}
