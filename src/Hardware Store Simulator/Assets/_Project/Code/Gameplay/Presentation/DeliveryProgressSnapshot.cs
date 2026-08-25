using System;
using System.Collections.Generic;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct DeliveryProgressSnapshot
    {
        public DeliveryProgressSnapshot(DeliveryLineProgressSnapshot[] lines,
            int stockedProductCount, int productCount)
        {
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));
            if (lines.Length == 0)
                throw new ArgumentException(
                    "An active delivery must contain at least one line.",
                    nameof(lines));
            if (productCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(productCount));
            if (stockedProductCount < 0 || stockedProductCount > productCount)
                throw new ArgumentOutOfRangeException(nameof(stockedProductCount));

            int calculatedStockedCount = 0;
            int calculatedProductCount = 0;
            int incompleteLineCount = 0;
            for (int index = 0; index < lines.Length; index++)
            {
                DeliveryLineProgressSnapshot line = lines[index];
                if (line.LineIndex != index)
                {
                    throw new ArgumentException(
                        "Delivery line indices must be contiguous and ordered.",
                        nameof(lines));
                }
                for (int previous = 0; previous < index; previous++)
                {
                    if (lines[previous].ProductType == line.ProductType)
                    {
                        throw new ArgumentException(
                            "A delivery cannot contain duplicate product types.",
                            nameof(lines));
                    }
                }

                calculatedStockedCount = checked(
                    calculatedStockedCount + line.StockedProductCount);
                calculatedProductCount = checked(
                    calculatedProductCount + line.ProductCount);
                if (line.StockedProductCount < line.ProductCount)
                    incompleteLineCount++;
            }
            if (calculatedStockedCount != stockedProductCount ||
                calculatedProductCount != productCount)
            {
                throw new ArgumentException(
                    "Delivery totals must equal the sum of its immutable lines.",
                    nameof(lines));
            }

            Lines = Array.AsReadOnly((DeliveryLineProgressSnapshot[])lines.Clone());
            StockedProductCount = stockedProductCount;
            ProductCount = productCount;
            IncompleteLineCount = incompleteLineCount;
        }

        public IReadOnlyList<DeliveryLineProgressSnapshot> Lines { get; }
        public int StockedProductCount { get; }
        public int ProductCount { get; }
        public int IncompleteLineCount { get; }
    }
}
