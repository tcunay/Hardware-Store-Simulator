using System;
using HardwareStore.Gameplay.Common.Collisions;
using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;
using Zenject;

namespace HardwareStore.Infrastructure.View
{
    [DisallowMultipleComponent]
    public class EntityBehaviour : MonoBehaviour, IEntityView
    {
        private GameEntity _entity;
        private ICollisionRegistry _collisionRegistry;
        private IEntityComponentRegistrar[] _registrars;
        private Collider[] _colliders;

        public GameEntity Entity => _entity ??
            throw new InvalidOperationException($"{name} is not bound to an ECS entity.");

        public bool HasEntity => _entity != null;

        [Inject]
        private void Construct(ICollisionRegistry collisionRegistry)
        {
            _collisionRegistry = collisionRegistry;
        }

        public void SetEntity(GameEntity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            if (_entity != null)
                throw new InvalidOperationException($"{name} is already bound to an ECS entity.");
            if (_collisionRegistry == null)
                throw new InvalidOperationException($"{name} must be injected before binding an ECS entity.");
            if (entity.hasView)
                throw new InvalidOperationException("The ECS entity is already bound to a view.");

            _entity = entity;
            _entity.AddView(this);
            _entity.Retain(this);

            CacheViewComponents();
            RegisterComponents();
            RegisterColliders();
        }

        public void ReleaseEntity()
        {
            if (_entity == null)
                throw new InvalidOperationException($"{name} is not bound to an ECS entity.");

            UnregisterComponents();
            UnregisterColliders();

            if (_entity.hasView)
            {
                if (!ReferenceEquals(_entity.View, this))
                    throw new InvalidOperationException("The ECS entity is bound to a different view.");

                _entity.RemoveView();
            }

            _entity.Release(this);
            _entity = null;
            _registrars = null;
            _colliders = null;
        }

        private void OnDestroy()
        {
            if (_entity == null)
                return;

            if (_entity.isEnabled)
                _entity.isDestructed = true;

            ReleaseEntity();
        }

        private void CacheViewComponents()
        {
            _registrars = GetComponentsInChildren<IEntityComponentRegistrar>(includeInactive: true);
            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
        }

        private void RegisterComponents()
        {
            foreach (IEntityComponentRegistrar registrar in _registrars)
                registrar.RegisterComponents();
        }

        private void UnregisterComponents()
        {
            foreach (IEntityComponentRegistrar registrar in _registrars)
                registrar.UnregisterComponents();
        }

        private void RegisterColliders()
        {
            foreach (Collider entityCollider in _colliders)
                _collisionRegistry.Register(entityCollider.GetEntityId(), _entity);
        }

        private void UnregisterColliders()
        {
            foreach (Collider entityCollider in _colliders)
                _collisionRegistry.Unregister(entityCollider.GetEntityId());
        }
    }
}
