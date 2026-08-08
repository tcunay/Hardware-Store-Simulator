using System;
using System.Collections.Generic;
using Entitas;
using UnityEngine;

namespace HardwareStore.Gameplay.Common.Collisions
{
    public sealed class CollisionRegistry : ICollisionRegistry
    {
        private readonly Dictionary<EntityId, IEntity> _entitiesByColliderId = new();

        public void Register(EntityId colliderId, IEntity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            if (_entitiesByColliderId.ContainsKey(colliderId))
                throw new InvalidOperationException($"Collider {colliderId} is already registered.");

            _entitiesByColliderId.Add(colliderId, entity);
        }

        public void Unregister(EntityId colliderId)
        {
            if (!_entitiesByColliderId.Remove(colliderId))
                throw new InvalidOperationException($"Collider {colliderId} is not registered.");
        }

        public bool TryGet<TEntity>(EntityId colliderId, out TEntity entity) where TEntity : class, IEntity
        {
            if (_entitiesByColliderId.TryGetValue(colliderId, out IEntity registered) &&
                registered is TEntity typedEntity)
            {
                entity = typedEntity;
                return true;
            }

            entity = null;
            return false;
        }
    }
}
