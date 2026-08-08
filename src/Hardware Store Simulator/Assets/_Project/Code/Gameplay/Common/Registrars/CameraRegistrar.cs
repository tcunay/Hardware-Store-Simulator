using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;

namespace HardwareStore.Gameplay.Common.Registrars
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRegistrar : EntityComponentRegistrar
    {
        public override void RegisterComponents()
        {
            Entity.AddCamera(GetComponent<Camera>());
        }

        public override void UnregisterComponents()
        {
            if (Entity.hasCamera)
                Entity.RemoveCamera();
        }
    }
}
