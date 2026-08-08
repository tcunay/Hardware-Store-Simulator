using Entitas;
using UnityEngine;

namespace HardwareStore.Gameplay.Common.Collisions
{
    public interface ICollisionRegistry
    {
        void Register(EntityId colliderId, IEntity entity);
        void Unregister(EntityId colliderId);
        bool TryGet<TEntity>(EntityId colliderId, out TEntity entity) where TEntity : class, IEntity;
    }
}
