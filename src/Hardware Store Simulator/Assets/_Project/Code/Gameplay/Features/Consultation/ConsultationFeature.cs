using HardwareStore.Gameplay.Features.Consultation.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Consultation
{
    public sealed class ConsultationFeature : Feature
    {
        public ConsultationFeature(ISystemFactory systems)
        {
            Add(systems.Create<ConfirmConsultationOfferSystem>());
            Add(systems.Create<CancelConsultationSystem>());
            Add(systems.Create<OpenConsultationSystem>());
        }
    }
}
