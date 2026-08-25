using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;

namespace HardwareStore.Gameplay.Registrars
{
    [DisallowMultipleComponent]
    public sealed class DriverExitAnchorRegistrar : EntityComponentRegistrar
    {
        public override void RegisterComponents()
        {
            Entity.AddDriverExitAnchor(transform);
        }

        public override void UnregisterComponents()
        {
            if (Entity.hasDriverExitAnchor)
                Entity.RemoveDriverExitAnchor();
        }
    }
}
