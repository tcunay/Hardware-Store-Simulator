using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;

namespace HardwareStore.Gameplay.Registrars
{
    [DisallowMultipleComponent]
    public sealed class ViewPivotRegistrar : EntityComponentRegistrar
    {
        public override void RegisterComponents()
        {
            Entity.AddViewPivot(transform);
        }

        public override void UnregisterComponents()
        {
            if (Entity.hasViewPivot)
                Entity.RemoveViewPivot();
        }
    }
}
