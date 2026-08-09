using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ClearInteractionPromptSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _promptedPlayers;
        private readonly IGroup<GameEntity> _availablePlayers;
        private readonly List<GameEntity> _promptBuffer = new(4);
        private readonly List<GameEntity> _availabilityBuffer = new(4);

        public ClearInteractionPromptSystem(GameContext gameContext)
        {
            _promptedPlayers = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.InteractionPrompt));
            _availablePlayers = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.FocusInteractionAvailable));
        }

        public void Execute()
        {
            foreach (GameEntity player in _promptedPlayers.GetEntities(_promptBuffer))
                player.RemoveInteractionPrompt();

            foreach (GameEntity player in _availablePlayers.GetEntities(_availabilityBuffer))
                player.isFocusInteractionAvailable = false;
        }
    }
}
