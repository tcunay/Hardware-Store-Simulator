using HardwareStore.Gameplay.Features.StoreDay.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.StoreDay
{
    public sealed class StoreDayFeature : Feature
    {
        public StoreDayFeature(ISystemFactory systems)
        {
            Add(systems.Create<TickStoreDayClockSystem>());
            Add(systems.Create<ReachStoreClosingTimeSystem>());
            Add(systems.Create<OpenStoreSystem>());
            Add(systems.Create<StartNextDaySystem>());
            Add(systems.Create<OpenDayReportSystem>());
            Add(systems.Create<ValidateStoreDayStateSystem>());
        }
    }
}
