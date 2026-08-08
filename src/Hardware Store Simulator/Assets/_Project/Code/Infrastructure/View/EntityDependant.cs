using System;
using UnityEngine;

namespace HardwareStore.Infrastructure.View
{
    public abstract class EntityDependant : MonoBehaviour
    {
        private EntityBehaviour _entityBehaviour;

        protected GameEntity Entity => ResolveEntityBehaviour().Entity;

        private EntityBehaviour ResolveEntityBehaviour()
        {
            if (_entityBehaviour != null)
                return _entityBehaviour;

            _entityBehaviour = GetComponentInParent<EntityBehaviour>(includeInactive: true);
            if (_entityBehaviour == null)
                throw new InvalidOperationException($"{name} requires an EntityBehaviour in its parent hierarchy.");

            return _entityBehaviour;
        }
    }
}
