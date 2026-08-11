using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Common.Economy
{
    public interface IProcurementSolvencyService
    {
        ProcurementPurchaseEvaluation EvaluatePurchase(
            int procurementTerminalEntityId,
            ProductTypeId productType);
    }
}
