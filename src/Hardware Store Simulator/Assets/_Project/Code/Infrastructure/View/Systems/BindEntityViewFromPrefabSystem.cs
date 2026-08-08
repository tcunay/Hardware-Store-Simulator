using System.Collections.Generic;
using Entitas;
using HardwareStore.Infrastructure.View.Factory;

namespace HardwareStore.Infrastructure.View.Systems
{
    public sealed class BindEntityViewFromPrefabSystem : IExecuteSystem
    {
        private readonly IEntityViewFactory _viewFactory;
        private readonly IGroup<GameEntity> _entities;
        private readonly List<GameEntity> _buffer = new(32);

        public BindEntityViewFromPrefabSystem(GameContext gameContext, IEntityViewFactory viewFactory)
        {
            _viewFactory = viewFactory;
            _entities = gameContext.GetGroup(GameMatcher
                .AllOf(
                    GameMatcher.ViewPrefab,
                    GameMatcher.SpawnPosition,
                    GameMatcher.SpawnRotation)
                .NoneOf(GameMatcher.View));
        }

        public void Execute()
        {
            foreach (GameEntity entity in _entities.GetEntities(_buffer))
            {
                _viewFactory.CreateViewFromPrefab(entity);
                entity.RemoveSpawnPosition();
                entity.RemoveSpawnRotation();
            }
        }
    }
}
