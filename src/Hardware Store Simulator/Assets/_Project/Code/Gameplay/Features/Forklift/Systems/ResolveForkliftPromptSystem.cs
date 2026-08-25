using System;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Features.Interaction.Systems;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Features.Forklift.Systems
{
    public sealed class ResolveForkliftPromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;

        public ResolveForkliftPromptSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Player,
                    GameMatcher.EntityId,
                    GameMatcher.StoreEntityId,
                    GameMatcher.FocusedEntityId,
                    GameMatcher.FocusedInteractionType)
                .NoneOf(GameMatcher.Destructed, GameMatcher.DrivingForklift));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                if (player.FocusedInteractionType != InteractionTypeId.Forklift)
                    continue;

                GameEntity forklift = _gameContext.GetEntityWithEntityId(
                    player.FocusedEntityId);
                if (forklift == null || !forklift.isForklift ||
                    !forklift.hasEntityId || !forklift.hasForkliftStoreEntityId ||
                    forklift.ForkliftStoreEntityId != player.StoreEntityId ||
                    !forklift.isInteractable ||
                    forklift.hasForkliftDriverEntityId || forklift.isDestructed)
                {
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} focuses an invalid forklift.");
                }

                player.SetInteractionPrompt(
                    LocalizedTexts.Text(
                        player.isHandsOccupied
                            ? LocalizationKey.PromptForkliftHandsOccupied
                            : LocalizationKey.PromptEnterForklift),
                    !player.isHandsOccupied);
            }
        }
    }
}
