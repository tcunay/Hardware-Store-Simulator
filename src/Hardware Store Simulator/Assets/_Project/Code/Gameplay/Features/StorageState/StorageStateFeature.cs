using HardwareStore.Gameplay.Features.StorageState.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.StorageState
{
    public sealed class StorageStateFeature : Feature
    {
        public StorageStateFeature(ISystemFactory systems)
        {
            Add(systems.Create<RefreshStorageOccupiedSlotCountSystem>());
            Add(systems.Create<RefreshOrderAvailableProductCountSystem>());
        }
    }
}
