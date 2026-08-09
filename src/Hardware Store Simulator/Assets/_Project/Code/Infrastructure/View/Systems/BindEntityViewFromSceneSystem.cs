using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Scene;
using HardwareStore.Infrastructure.View.Factory;

namespace HardwareStore.Infrastructure.View.Systems
{
    public sealed class BindEntityViewFromSceneSystem : IExecuteSystem
    {
        private readonly IStoreSceneData _sceneData;
        private readonly IEntityViewFactory _viewFactory;
        private readonly IGroup<GameEntity> _entities;
        private readonly List<GameEntity> _buffer = new(8);

        public BindEntityViewFromSceneSystem(GameContext gameContext, IStoreSceneData sceneData,
            IEntityViewFactory viewFactory)
        {
            _sceneData = sceneData;
            _viewFactory = viewFactory;
            _entities = gameContext.GetGroup(GameMatcher
                .AllOf(GameMatcher.EntityId, GameMatcher.SceneViewKey)
                .NoneOf(GameMatcher.View, GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity entity in _entities.GetEntities(_buffer))
            {
                if (entity.hasViewPrefab || entity.hasSpawnPosition || entity.hasSpawnRotation)
                    throw new InvalidOperationException(
                        $"Entity {entity.EntityId} mixes scene and runtime-prefab view binding data.");

                EntityBehaviour view = _sceneData.GetSceneView(entity.SceneViewKey);
                _viewFactory.BindExistingView(entity, view);
                entity.RemoveSceneViewKey();
            }
        }
    }
}
