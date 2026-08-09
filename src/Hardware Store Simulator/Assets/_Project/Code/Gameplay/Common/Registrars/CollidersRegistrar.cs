using System;
using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;

namespace HardwareStore.Gameplay.Common.Registrars
{
    [DisallowMultipleComponent]
    public sealed class CollidersRegistrar : EntityComponentRegistrar
    {
        public override void RegisterComponents()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(includeInactive: true);
            if (colliders.Length == 0)
                throw new InvalidOperationException($"{name} has no colliders to register.");

            Entity.AddColliders(colliders);
        }

        public override void UnregisterComponents()
        {
            if (Entity.hasColliders)
                Entity.RemoveColliders();
        }
    }
}
