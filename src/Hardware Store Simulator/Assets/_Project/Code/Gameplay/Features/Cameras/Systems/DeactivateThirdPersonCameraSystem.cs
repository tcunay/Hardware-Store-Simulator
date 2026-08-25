using System.Collections.Generic;
using Entitas;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Cameras.Systems
{
    public sealed class DeactivateThirdPersonCameraSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _players;
        private readonly List<GameEntity> _buffer = new(1);

        public DeactivateThirdPersonCameraSystem(GameContext gameContext)
        {
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Player,
                    GameMatcher.Camera,
                    GameMatcher.ViewPivot,
                    GameMatcher.ViewPitch,
                    GameMatcher.CameraOrbitYaw,
                    GameMatcher.ThirdPersonCameraActive)
                .NoneOf(GameMatcher.DrivingForklift));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players.GetEntities(_buffer))
            {
                Transform cameraTransform = player.Camera.transform;
                if (cameraTransform.parent != player.ViewPivot)
                {
                    throw new System.InvalidOperationException(
                        "First-person camera restoration requires Camera under ViewPivot.");
                }

                player.ViewPivot.localRotation = Quaternion.identity;
                cameraTransform.SetLocalPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                player.ReplaceCameraOrbitYaw(0f);
                player.ReplaceViewPitch(0f);
                player.isThirdPersonCameraActive = false;
            }
        }
    }
}
