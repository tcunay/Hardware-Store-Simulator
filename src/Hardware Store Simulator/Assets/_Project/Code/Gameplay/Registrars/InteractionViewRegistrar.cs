using HardwareStore.Gameplay.Views;
using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;

namespace HardwareStore.Gameplay.Registrars
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionView))]
    public sealed class InteractionViewRegistrar : EntityComponentRegistrar
    {
        public override void RegisterComponents()
        {
            Entity.AddInteractionView(GetComponent<InteractionView>());
        }

        public override void UnregisterComponents()
        {
            if (Entity.hasInteractionView)
                Entity.RemoveInteractionView();
        }
    }
}
