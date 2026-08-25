using System;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct ProcurementCartLineSnapshot
    {
        public ProcurementCartLineSnapshot(
            int lineIndex,
            ProductTypeId productType,
            int packageCount,
            int packageProductCount,
            int packageCost,
            int productCount,
            int lineCost)
        {
            if (lineIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(lineIndex));
            if (!Enum.IsDefined(typeof(ProductTypeId), productType))
                throw new ArgumentOutOfRangeException(nameof(productType));
            if (packageCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(packageCount));
            if (packageProductCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(packageProductCount));
            if (packageCost <= 0)
                throw new ArgumentOutOfRangeException(nameof(packageCost));
            if (productCount != checked(packageCount * packageProductCount))
            {
                throw new ArgumentException(
                    "Cart line product count must match its package composition.",
                    nameof(productCount));
            }
            if (lineCost != checked(packageCount * packageCost))
            {
                throw new ArgumentException(
                    "Cart line cost must match its package composition.",
                    nameof(lineCost));
            }

            LineIndex = lineIndex;
            ProductType = productType;
            PackageCount = packageCount;
            PackageProductCount = packageProductCount;
            PackageCost = packageCost;
            ProductCount = productCount;
            LineCost = lineCost;
        }

        public int LineIndex { get; }
        public ProductTypeId ProductType { get; }
        public int PackageCount { get; }
        public int PackageProductCount { get; }
        public int PackageCost { get; }
        public int ProductCount { get; }
        public int LineCost { get; }
    }
}
