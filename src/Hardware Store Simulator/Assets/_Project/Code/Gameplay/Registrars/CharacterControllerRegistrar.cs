using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;

namespace HardwareStore.Gameplay.Registrars
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterControllerRegistrar : EntityComponentRegistrar
    {
        public override void RegisterComponents()
        {
            Entity.AddCharacterController(GetComponent<CharacterController>());
        }

        public override void UnregisterComponents()
        {
            if (Entity.hasCharacterController)
                Entity.RemoveCharacterController();
        }
    }
}
