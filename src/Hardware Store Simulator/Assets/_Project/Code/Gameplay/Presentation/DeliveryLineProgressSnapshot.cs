using System;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct DeliveryLineProgressSnapshot
    {
        public DeliveryLineProgressSnapshot(int lineIndex, ProductTypeId productType,
            int stockedProductCount, int productCount)
        {
            if (lineIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(lineIndex));
            if (!Enum.IsDefined(typeof(ProductTypeId), productType))
                throw new ArgumentOutOfRangeException(nameof(productType));
            if (productCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(productCount));
            if (stockedProductCount < 0 || stockedProductCount > productCount)
                throw new ArgumentOutOfRangeException(nameof(stockedProductCount));

            LineIndex = lineIndex;
            ProductType = productType;
            StockedProductCount = stockedProductCount;
            ProductCount = productCount;
        }

        public int LineIndex { get; }
        public ProductTypeId ProductType { get; }
        public int StockedProductCount { get; }
        public int ProductCount { get; }
    }
}
