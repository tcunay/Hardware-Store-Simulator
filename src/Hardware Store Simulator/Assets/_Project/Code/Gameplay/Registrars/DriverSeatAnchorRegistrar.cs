using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;

namespace HardwareStore.Gameplay.Registrars
{
    [DisallowMultipleComponent]
    public sealed class DriverSeatAnchorRegistrar : EntityComponentRegistrar
    {
        public override void RegisterComponents()
        {
            Entity.AddDriverSeatAnchor(transform);
        }

        public override void UnregisterComponents()
        {
            if (Entity.hasDriverSeatAnchor)
                Entity.RemoveDriverSeatAnchor();
        }
    }
}
