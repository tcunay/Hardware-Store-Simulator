using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;

namespace HardwareStore.Gameplay.Registrars
{
    [DisallowMultipleComponent]
    public sealed class CargoAnchorRegistrar : EntityComponentRegistrar
    {
        public override void RegisterComponents()
        {
            Entity.AddCargoAnchor(transform);
        }

        public override void UnregisterComponents()
        {
            if (Entity.hasCargoAnchor)
                Entity.RemoveCargoAnchor();
        }
    }
}
