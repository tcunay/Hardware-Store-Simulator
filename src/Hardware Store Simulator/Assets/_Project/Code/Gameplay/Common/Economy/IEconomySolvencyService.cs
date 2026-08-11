namespace HardwareStore.Gameplay.Common.Economy
{
    public interface IEconomySolvencyService
    {
        EconomyDebitEvaluation EvaluateDebit(int storeEntityId, int debitAmount);
    }
}
