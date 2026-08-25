using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Common.Economy
{
    public interface IProcurementSolvencyService
    {
        ProcurementPurchaseEvaluation EvaluateCart(int procurementCartEntityId);

        ProcurementPurchaseEvaluation EvaluatePurchase(
            int procurementTerminalEntityId,
            ProductTypeId productType);
    }
}
