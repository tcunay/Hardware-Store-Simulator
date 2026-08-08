using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;

namespace HardwareStore.Gameplay.Registrars
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RigidbodyRegistrar : EntityComponentRegistrar
    {
        public override void RegisterComponents()
        {
            Entity.AddRigidbody(GetComponent<Rigidbody>());
        }

        public override void UnregisterComponents()
        {
            if (Entity.hasRigidbody)
                Entity.RemoveRigidbody();
        }
    }
}
