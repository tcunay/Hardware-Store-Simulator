using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;

namespace HardwareStore.Gameplay.Registrars
{
    [DisallowMultipleComponent]
    public sealed class DropOriginRegistrar : EntityComponentRegistrar
    {
        public override void RegisterComponents()
        {
            Entity.AddDropOrigin(transform);
        }

        public override void UnregisterComponents()
        {
            if (Entity.hasDropOrigin)
                Entity.RemoveDropOrigin();
        }
    }
}
