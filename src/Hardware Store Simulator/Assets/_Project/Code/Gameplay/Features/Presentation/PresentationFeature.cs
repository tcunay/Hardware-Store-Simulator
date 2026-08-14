using HardwareStore.Gameplay.Features.Presentation.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Presentation
{
    public sealed class PresentationFeature : Feature
    {
        public PresentationFeature(ISystemFactory systems)
        {
            Add(systems.Create<PresentInteractionHighlightsSystem>());
            Add(systems.Create<PresentCustomerDissatisfactionSystem>());
            Add(systems.Create<PresentHudSystem>());
            Add(systems.Create<PresentDayNightSystem>());
            Add(systems.Create<PresentDayReportSystem>());
            Add(systems.Create<PresentProcurementSystem>());
            Add(systems.Create<PresentConsultationSystem>());
            Add(systems.Create<PlayAudioCuesSystem>());
            Add(systems.Create<PresentCustomerPatienceEventsSystem>());
            Add(systems.Create<PresentNotificationsSystem>());
        }
    }
}
