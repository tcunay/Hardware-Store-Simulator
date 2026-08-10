using Entitas;
using HardwareStore.Gameplay.Common.Time;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Player.Systems
{
    public sealed class ApplyLookInputSystem : IExecuteSystem
    {
        private readonly ITimeService _time;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;

        public ApplyLookInputSystem(GameContext gameContext, InputContext inputContext, ITimeService time)
        {
            _time = time;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.Transform,
                GameMatcher.ViewPivot,
                GameMatcher.ViewPitch,
                GameMatcher.MouseSensitivity,
                GameMatcher.GamepadLookSpeed,
                GameMatcher.MaxPitch,
                GameMatcher.CursorLocked)
                .NoneOf(GameMatcher.ConsultationVisitEntityId));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(
                InputMatcher.InputState,
                InputMatcher.LookInput));
        }

        public void Execute()
        {
            foreach (InputEntity input in _inputs)
            foreach (GameEntity player in _players)
            {
                Vector2 look = input.LookInput;
                float multiplier = input.isPointerLook
                    ? player.MouseSensitivity
                    : player.GamepadLookSpeed * _time.DeltaTime;

                player.Transform.Rotate(Vector3.up, look.x * multiplier, Space.World);
                float pitch = Mathf.Clamp(player.ViewPitch - look.y * multiplier,
                    -player.MaxPitch, player.MaxPitch);
                player.ViewPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                player.ReplaceViewPitch(pitch);
            }
        }
    }
}
