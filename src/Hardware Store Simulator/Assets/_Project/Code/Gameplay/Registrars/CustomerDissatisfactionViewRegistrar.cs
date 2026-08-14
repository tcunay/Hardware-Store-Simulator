using HardwareStore.Gameplay.Views;
using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;

namespace HardwareStore.Gameplay.Registrars
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CustomerDissatisfactionView))]
    public sealed class CustomerDissatisfactionViewRegistrar : EntityComponentRegistrar
    {
        public override void RegisterComponents()
        {
            Entity.AddCustomerDissatisfactionView(
                GetComponent<CustomerDissatisfactionView>());
        }

        public override void UnregisterComponents()
        {
            if (Entity.hasCustomerDissatisfactionView)
                Entity.RemoveCustomerDissatisfactionView();
        }
    }
}
