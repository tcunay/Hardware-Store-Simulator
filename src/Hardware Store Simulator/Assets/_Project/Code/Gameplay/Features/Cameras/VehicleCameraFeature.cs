using HardwareStore.Gameplay.Features.Cameras.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Cameras
{
    public sealed class VehicleCameraFeature : Feature
    {
        public VehicleCameraFeature(ISystemFactory systems)
        {
            Add(systems.Create<ActivateThirdPersonCameraSystem>());
            Add(systems.Create<UpdateThirdPersonCameraSystem>());
            Add(systems.Create<DeactivateThirdPersonCameraSystem>());
        }
    }
}
