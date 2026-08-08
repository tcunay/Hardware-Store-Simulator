using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Cleanup.Systems
{
    public sealed class CleanupDestructedEntitiesSystem : ICleanupSystem
    {
        private readonly IGroup<GameEntity> _entities;
        private readonly List<GameEntity> _buffer = new(32);

        public CleanupDestructedEntitiesSystem(GameContext gameContext) =>
            _entities = gameContext.GetGroup(GameMatcher.AllOf(GameMatcher.Destructed));

        public void Cleanup()
        {
            foreach (GameEntity entity in _entities.GetEntities(_buffer))
                entity.Destroy();
        }
    }
}
