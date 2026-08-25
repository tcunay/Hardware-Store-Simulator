using System;
using Entitas;
using HardwareStore.Gameplay.Common.Physics;
using HardwareStore.Gameplay.Common.Time;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Cameras.Systems
{
    public sealed class UpdateThirdPersonCameraSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IThirdPersonCameraCollisionService _collision;
        private readonly ITimeService _time;
        private readonly PlayerConfig _config;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;

        public UpdateThirdPersonCameraSystem(
            GameContext gameContext,
            InputContext inputContext,
            IThirdPersonCameraCollisionService collision,
            ITimeService time,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _collision = collision;
            _time = time;
            _config = staticData.Player;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Player,
                    GameMatcher.EntityId,
                    GameMatcher.Transform,
                    GameMatcher.DrivingForklift,
                    GameMatcher.ThirdPersonCameraActive,
                    GameMatcher.Camera,
                    GameMatcher.ViewPivot,
                    GameMatcher.ViewPitch,
                    GameMatcher.CameraOrbitYaw,
                    GameMatcher.MouseSensitivity,
                    GameMatcher.GamepadLookSpeed)
                .NoneOf(GameMatcher.Destructed));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(
                InputMatcher.InputState,
                InputMatcher.LookInput));
        }

        public void Execute()
        {
            InputEntity input = ResolveInput();
            foreach (GameEntity player in _players)
            {
                GameEntity forklift = ResolveForklift(player);
                if (player.isCursorLocked)
                    ApplyLook(player, input);
                PresentCamera(player, forklift);
            }
        }

        private InputEntity ResolveInput()
        {
            InputEntity[] inputs = _inputs.GetEntities();
            if (inputs.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Third-person camera requires one input state, found {inputs.Length}.");
            }

            return inputs[0];
        }

        private GameEntity ResolveForklift(GameEntity player)
        {
            GameEntity forklift =
                _gameContext.GetEntityWithForkliftDriverEntityId(player.EntityId);
            if (forklift == null || !forklift.isForklift ||
                !forklift.hasTransform || forklift.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Third-person camera cannot resolve the vehicle for player " +
                    $"{player.EntityId}.");
            }

            return forklift;
        }

        private void ApplyLook(GameEntity player, InputEntity input)
        {
            Vector2 look = input.LookInput;
            float multiplier = input.isPointerLook
                ? player.MouseSensitivity
                : player.GamepadLookSpeed * _time.DeltaTime;
            float yaw = Mathf.Repeat(
                player.CameraOrbitYaw + look.x * multiplier + 180f,
                360f) - 180f;
            float pitch = Mathf.Clamp(
                player.ViewPitch - look.y * multiplier,
                _config.VehicleCameraMinPitch,
                _config.VehicleCameraMaxPitch);
            player.ReplaceCameraOrbitYaw(yaw);
            player.ReplaceViewPitch(pitch);
        }

        private void PresentCamera(GameEntity player, GameEntity forklift)
        {
            Transform pivot = player.ViewPivot;
            Transform cameraTransform = player.Camera.transform;
            if (cameraTransform.parent != pivot)
            {
                throw new InvalidOperationException(
                    "Third-person camera requires the player camera directly under ViewPivot.");
            }

            pivot.localRotation = Quaternion.Euler(
                player.ViewPitch,
                player.CameraOrbitYaw,
                0f);
            Vector3 desiredPosition = pivot.position -
                                      pivot.forward *
                                      _config.VehicleCameraDistance;
            Vector3 resolvedPosition = _collision.ResolvePosition(
                pivot.position,
                desiredPosition,
                _config.VehicleCameraCollisionRadius,
                _config.VehicleCameraCollisionPadding,
                player.Transform,
                forklift.Transform);
            cameraTransform.SetPositionAndRotation(
                resolvedPosition,
                pivot.rotation);
        }
    }
}
