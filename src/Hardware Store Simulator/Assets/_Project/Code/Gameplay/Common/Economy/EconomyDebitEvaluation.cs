using System;

namespace HardwareStore.Gameplay.Common.Economy
{
    public readonly struct EconomyDebitEvaluation
    {
        public EconomyDebitEvaluation(
            EconomyDebitAvailability availability,
            int moneyAfterDebit)
        {
            if (!Enum.IsDefined(typeof(EconomyDebitAvailability), availability))
                throw new ArgumentOutOfRangeException(nameof(availability));

            Availability = availability;
            MoneyAfterDebit = moneyAfterDebit;
        }

        public EconomyDebitAvailability Availability { get; }
        public int MoneyAfterDebit { get; }
        public bool CanDebit => Availability == EconomyDebitAvailability.Available;
    }
}
