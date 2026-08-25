using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Cameras.Systems
{
    public sealed class ActivateThirdPersonCameraSystem : IExecuteSystem
    {
        private readonly PlayerConfig _config;
        private readonly IGroup<GameEntity> _players;
        private readonly List<GameEntity> _buffer = new(1);

        public ActivateThirdPersonCameraSystem(
            GameContext gameContext,
            IStaticDataService staticData)
        {
            _config = staticData.Player;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Player,
                    GameMatcher.DrivingForklift,
                    GameMatcher.Camera,
                    GameMatcher.ViewPivot,
                    GameMatcher.ViewPitch,
                    GameMatcher.CameraOrbitYaw)
                .NoneOf(
                    GameMatcher.ThirdPersonCameraActive,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players.GetEntities(_buffer))
            {
                ValidateCameraHierarchy(player);
                player.ReplaceCameraOrbitYaw(0f);
                player.ReplaceViewPitch(_config.VehicleCameraDefaultPitch);
                player.ViewPivot.localRotation = Quaternion.Euler(
                    _config.VehicleCameraDefaultPitch,
                    0f,
                    0f);
                player.isThirdPersonCameraActive = true;
            }
        }

        private static void ValidateCameraHierarchy(GameEntity player)
        {
            if (player.Camera == null || player.ViewPivot == null ||
                player.Camera.transform.parent != player.ViewPivot)
            {
                throw new System.InvalidOperationException(
                    "Third-person camera requires the player camera directly under ViewPivot.");
            }
        }
    }
}
