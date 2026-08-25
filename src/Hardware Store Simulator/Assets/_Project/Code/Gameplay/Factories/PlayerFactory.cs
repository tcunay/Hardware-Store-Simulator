using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class PlayerFactory : IPlayerFactory
    {
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;

        public PlayerFactory(IIdentifierService identifiers, IStaticDataService staticData)
        {
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public GameEntity Create(Pose at, int storeEntityId)
        {
            PlayerConfig player = _staticData.Player;
            InteractionConfig interaction = _staticData.Interaction;

            GameEntity entity = CreateEntity.Empty(_identifiers.Next())
                .AddStoreEntityId(storeEntityId)
                .AddViewPrefab(player.ViewPrefab)
                .AddSpawnPosition(at.position)
                .AddSpawnRotation(at.rotation)
                .AddWalkSpeed(player.WalkSpeed)
                .AddSprintSpeed(player.SprintSpeed)
                .AddGravity(player.Gravity)
                .AddVerticalVelocity(0f)
                .AddHorizontalSpeed(0f)
                .AddMoveDirection(Vector3.zero)
                .AddMovementSpeed(player.WalkSpeed)
                .AddViewPitch(0f)
                .AddCameraOrbitYaw(0f)
                .AddMouseSensitivity(player.MouseSensitivity)
                .AddGamepadLookSpeed(player.GamepadLookSpeed)
                .AddMaxPitch(player.MaxPitch)
                .AddInteractionDistance(interaction.Distance)
                .AddAimAssistRadius(interaction.AimAssistRadius)
                .With(x => x.isPlayer = true)
                .With(x => x.isCursorLocked = true);

            return entity;
        }
    }
}
