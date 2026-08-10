using System;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct ConsultationOfferSnapshot
    {
        public ConsultationOfferSnapshot(int index, string title, string description,
            string productDisplayName, string productUnitLabel, int requiredProductCount,
            int availableProductCount, int revenue, int expectedProfit, bool selected)
        {
            Index = index;
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            ProductDisplayName = productDisplayName ??
                                 throw new ArgumentNullException(nameof(productDisplayName));
            ProductUnitLabel = productUnitLabel ??
                               throw new ArgumentNullException(nameof(productUnitLabel));
            RequiredProductCount = requiredProductCount;
            AvailableProductCount = availableProductCount;
            Revenue = revenue;
            ExpectedProfit = expectedProfit;
            Selected = selected;
        }

        public int Index { get; }
        public string Title { get; }
        public string Description { get; }
        public string ProductDisplayName { get; }
        public string ProductUnitLabel { get; }
        public int RequiredProductCount { get; }
        public int AvailableProductCount { get; }
        public int Revenue { get; }
        public int ExpectedProfit { get; }
        public bool Selected { get; }
    }
}
