using System.Collections.Generic;
using Entitas;
using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Cleanup.Systems
{
    public sealed class CleanupDestructedViewsSystem : ICleanupSystem
    {
        private readonly IGroup<GameEntity> _entities;
        private readonly List<GameEntity> _buffer = new(32);

        public CleanupDestructedViewsSystem(GameContext gameContext) =>
            _entities = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Destructed,
                GameMatcher.View));

        public void Cleanup()
        {
            foreach (GameEntity entity in _entities.GetEntities(_buffer))
            {
                IEntityView view = entity.View;
                view.ReleaseEntity();
                Object.Destroy(view.gameObject);
            }
        }
    }
}
