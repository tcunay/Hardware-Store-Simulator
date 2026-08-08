using HardwareStore.Gameplay.Views;
using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;

namespace HardwareStore.Gameplay.Registrars
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProductView))]
    public sealed class ProductViewRegistrar : EntityComponentRegistrar
    {
        public override void RegisterComponents()
        {
            Entity.AddProductView(GetComponent<ProductView>());
        }

        public override void UnregisterComponents()
        {
            if (Entity.hasProductView)
                Entity.RemoveProductView();
        }
    }
}
