using Entitas;
using HardwareStore.Gameplay.Common.Time;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Movement.Systems
{
    public sealed class MoveCharacterControllerSystem : IExecuteSystem
    {
        private readonly ITimeService _time;
        private readonly IGroup<GameEntity> _players;

        public MoveCharacterControllerSystem(GameContext gameContext, ITimeService time)
        {
            _time = time;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.CharacterController,
                GameMatcher.VerticalVelocity,
                GameMatcher.Gravity,
                GameMatcher.MoveDirection,
                GameMatcher.MovementSpeed,
                GameMatcher.HorizontalSpeed));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                CharacterController controller = player.CharacterController;
                float verticalVelocity = player.VerticalVelocity;
                if (controller.isGrounded && verticalVelocity < 0f)
                    verticalVelocity = -2f;

                verticalVelocity += player.Gravity * _time.DeltaTime;
                Vector3 planarVelocity = player.MoveDirection * player.MovementSpeed;
                controller.Move((planarVelocity + Vector3.up * verticalVelocity) * _time.DeltaTime);
                player.ReplaceVerticalVelocity(verticalVelocity);
                player.ReplaceHorizontalSpeed(planarVelocity.magnitude);
            }
        }
    }
}
