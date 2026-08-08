using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Cleanup.Systems
{
    public sealed class ReleaseEntityViewsSystem : ITearDownSystem
    {
        private readonly IGroup<GameEntity> _entities;
        private readonly List<GameEntity> _buffer = new(32);

        public ReleaseEntityViewsSystem(GameContext gameContext) =>
            _entities = gameContext.GetGroup(GameMatcher.AllOf(GameMatcher.View));

        public void TearDown()
        {
            foreach (GameEntity entity in _entities.GetEntities(_buffer))
                entity.View.ReleaseEntity();
        }
    }
}
