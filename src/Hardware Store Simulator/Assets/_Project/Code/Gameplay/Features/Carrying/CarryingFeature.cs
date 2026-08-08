using HardwareStore.Gameplay.Features.Carrying.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Carrying
{
    public sealed class CarryingFeature : Feature
    {
        public CarryingFeature(ISystemFactory systems)
        {
            Add(systems.Create<DropHeldProductSystem>());
            Add(systems.Create<PickUpProductSystem>());
            Add(systems.Create<LoadHeldProductSystem>());
        }
    }
}
