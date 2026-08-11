using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class UpdateFocusHighlightSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<GameEntity> _highlighted;
        private readonly List<GameEntity> _buffer = new(8);

        public UpdateFocusHighlightSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _players = gameContext.GetGroup(GameMatcher.AllOf(GameMatcher.Player)
                .NoneOf(GameMatcher.ModalOpen));
            _highlighted = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Highlighted,
                GameMatcher.EntityId));
        }

        public void Execute()
        {
            foreach (GameEntity entity in _highlighted.GetEntities(_buffer))
                entity.isHighlighted = false;

            foreach (GameEntity player in _players)
            {
                if (player.hasFocusedEntityId)
                    _gameContext.GetEntityWithEntityId(player.FocusedEntityId).isHighlighted = true;
            }
        }
    }
}
