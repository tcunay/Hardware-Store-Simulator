using System;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct DayReportSnapshot
    {
        public DayReportSnapshot(int dayNumber, int openingBalance, int revenue,
            int procurementExpenses, int upgradeExpenses, int payrollExpenses,
            int closingBalance, int completedOrderCount, int lostCustomerCount,
            int storageProductCount)
        {
            if (dayNumber <= 0)
                throw new ArgumentOutOfRangeException(nameof(dayNumber));
            if (openingBalance < 0)
                throw new ArgumentOutOfRangeException(nameof(openingBalance));
            if (revenue < 0)
                throw new ArgumentOutOfRangeException(nameof(revenue));
            if (procurementExpenses < 0)
                throw new ArgumentOutOfRangeException(nameof(procurementExpenses));
            if (upgradeExpenses < 0)
                throw new ArgumentOutOfRangeException(nameof(upgradeExpenses));
            if (payrollExpenses < 0)
                throw new ArgumentOutOfRangeException(nameof(payrollExpenses));
            if (closingBalance < 0)
                throw new ArgumentOutOfRangeException(nameof(closingBalance));
            if (completedOrderCount < 0)
                throw new ArgumentOutOfRangeException(nameof(completedOrderCount));
            if (lostCustomerCount < 0)
                throw new ArgumentOutOfRangeException(nameof(lostCustomerCount));
            if (storageProductCount < 0)
                throw new ArgumentOutOfRangeException(nameof(storageProductCount));

            long netCashFlow =
                (long)revenue - procurementExpenses - upgradeExpenses - payrollExpenses;
            if (netCashFlow < int.MinValue || netCashFlow > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(revenue));
            if ((long)openingBalance + netCashFlow != closingBalance)
            {
                throw new ArgumentException(
                    "Closing balance must equal opening balance plus the reported cash flow.",
                    nameof(closingBalance));
            }

            DayNumber = dayNumber;
            OpeningBalance = openingBalance;
            Revenue = revenue;
            ProcurementExpenses = procurementExpenses;
            UpgradeExpenses = upgradeExpenses;
            PayrollExpenses = payrollExpenses;
            NetCashFlow = (int)netCashFlow;
            ClosingBalance = closingBalance;
            CompletedOrderCount = completedOrderCount;
            LostCustomerCount = lostCustomerCount;
            StorageProductCount = storageProductCount;
        }

        public int DayNumber { get; }
        public int OpeningBalance { get; }
        public int Revenue { get; }
        public int ProcurementExpenses { get; }
        public int UpgradeExpenses { get; }
        public int PayrollExpenses { get; }
        public int NetCashFlow { get; }
        public int ClosingBalance { get; }
        public int CompletedOrderCount { get; }
        public int LostCustomerCount { get; }
        public int StorageProductCount { get; }
    }
}
