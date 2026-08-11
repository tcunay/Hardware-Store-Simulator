using System;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct OrderLineSnapshot
    {
        public OrderLineSnapshot(int lineIndex, ProductTypeId productType,
            int availableProductCount, int loadedProductCount,
            int requiredProductCount)
        {
            if (lineIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(lineIndex));
            if (availableProductCount < 0)
                throw new ArgumentOutOfRangeException(nameof(availableProductCount));
            if (loadedProductCount < 0)
                throw new ArgumentOutOfRangeException(nameof(loadedProductCount));
            if (requiredProductCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredProductCount));
            if (loadedProductCount > requiredProductCount)
                throw new ArgumentOutOfRangeException(nameof(loadedProductCount));

            LineIndex = lineIndex;
            ProductType = productType;
            AvailableProductCount = availableProductCount;
            LoadedProductCount = loadedProductCount;
            RequiredProductCount = requiredProductCount;
        }

        public int LineIndex { get; }
        public ProductTypeId ProductType { get; }
        public int AvailableProductCount { get; }
        public int LoadedProductCount { get; }
        public int RequiredProductCount { get; }
    }
}
