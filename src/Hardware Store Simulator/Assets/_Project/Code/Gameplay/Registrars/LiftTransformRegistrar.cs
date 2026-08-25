using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;

namespace HardwareStore.Gameplay.Registrars
{
    [DisallowMultipleComponent]
    public sealed class LiftTransformRegistrar : EntityComponentRegistrar
    {
        public override void RegisterComponents()
        {
            Entity.AddLiftTransform(transform);
        }

        public override void UnregisterComponents()
        {
            if (Entity.hasLiftTransform)
                Entity.RemoveLiftTransform();
        }
    }
}
