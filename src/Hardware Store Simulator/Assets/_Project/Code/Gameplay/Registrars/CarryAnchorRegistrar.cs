using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;

namespace HardwareStore.Gameplay.Registrars
{
    [DisallowMultipleComponent]
    public sealed class CarryAnchorRegistrar : EntityComponentRegistrar
    {
        public override void RegisterComponents()
        {
            Entity.AddCarryAnchor(transform);
        }

        public override void UnregisterComponents()
        {
            if (Entity.hasCarryAnchor)
                Entity.RemoveCarryAnchor();
        }
    }
}
