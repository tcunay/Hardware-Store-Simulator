using System;
using System.Collections.Generic;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct ConsultationOfferSnapshot
    {
        public ConsultationOfferSnapshot(int index, ConsultationOfferLineSnapshot[] lines,
            int totalUnitCount,
            int productCost, int orderReward, int expectedProfit, bool selected)
        {
            Index = index;
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));
            if (lines.Length == 0)
                throw new ArgumentException(
                    "A consultation offer must contain at least one product line.",
                    nameof(lines));
            if (totalUnitCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(totalUnitCount));
            if (productCost < 0)
                throw new ArgumentOutOfRangeException(nameof(productCost));
            if (orderReward < 0)
                throw new ArgumentOutOfRangeException(nameof(orderReward));

            Lines = Array.AsReadOnly((ConsultationOfferLineSnapshot[])lines.Clone());
            TotalUnitCount = totalUnitCount;
            ProductCost = productCost;
            OrderReward = orderReward;
            ExpectedProfit = expectedProfit;
            Selected = selected;
        }

        public int Index { get; }
        public IReadOnlyList<ConsultationOfferLineSnapshot> Lines { get; }
        public int TotalUnitCount { get; }
        public int ProductCost { get; }
        public int OrderReward { get; }
        public int ExpectedProfit { get; }
        public bool Selected { get; }
    }
}
