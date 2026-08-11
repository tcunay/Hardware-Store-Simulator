using System;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct ConsultationOfferLineSnapshot
    {
        public ConsultationOfferLineSnapshot(int lineIndex, ProductTypeId productType,
            string productDisplayName, string productUnitLabel,
            int availableProductCount, int requiredProductCount)
        {
            if (lineIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(lineIndex));
            if (availableProductCount < 0)
                throw new ArgumentOutOfRangeException(nameof(availableProductCount));
            if (requiredProductCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredProductCount));

            LineIndex = lineIndex;
            ProductType = productType;
            ProductDisplayName = productDisplayName ??
                                 throw new ArgumentNullException(nameof(productDisplayName));
            ProductUnitLabel = productUnitLabel ??
                               throw new ArgumentNullException(nameof(productUnitLabel));
            AvailableProductCount = availableProductCount;
            RequiredProductCount = requiredProductCount;
        }

        public int LineIndex { get; }
        public ProductTypeId ProductType { get; }
        public string ProductDisplayName { get; }
        public string ProductUnitLabel { get; }
        public int AvailableProductCount { get; }
        public int RequiredProductCount { get; }
    }
}
