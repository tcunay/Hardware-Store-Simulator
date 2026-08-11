using HardwareStore.Gameplay.Features.Procurement.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Procurement
{
    public sealed class ProcurementFeature : Feature
    {
        public ProcurementFeature(ISystemFactory systems)
        {
            Add(systems.Create<ChangeProcurementSelectionSystem>());
            Add(systems.Create<EmitPurchaseDeliveryRequestSystem>());
            Add(systems.Create<CancelProcurementSystem>());
            Add(systems.Create<OpenProcurementSystem>());
        }
    }
}
