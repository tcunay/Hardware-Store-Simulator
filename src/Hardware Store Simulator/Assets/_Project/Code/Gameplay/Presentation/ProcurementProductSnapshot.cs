using System;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct ProcurementProductSnapshot
    {
        public ProcurementProductSnapshot(
            int index,
            ProductTypeId productType,
            int packageProductCount,
            int packageCost,
            int stockProductCount,
            int inTransitProductCount,
            int minimumRequiredProductCount,
            int maximumRequiredProductCount,
            int remainingRequiredProductCount,
            int projectedDeficitProductCount,
            int cartPackageCount)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (!Enum.IsDefined(typeof(ProductTypeId), productType))
                throw new ArgumentOutOfRangeException(nameof(productType));
            if (packageProductCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(packageProductCount));
            if (packageCost <= 0)
                throw new ArgumentOutOfRangeException(nameof(packageCost));
            if (stockProductCount < 0)
                throw new ArgumentOutOfRangeException(nameof(stockProductCount));
            if (inTransitProductCount < 0)
                throw new ArgumentOutOfRangeException(nameof(inTransitProductCount));
            if (minimumRequiredProductCount < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumRequiredProductCount));
            if (maximumRequiredProductCount < minimumRequiredProductCount)
                throw new ArgumentOutOfRangeException(nameof(maximumRequiredProductCount));
            if (remainingRequiredProductCount < 0)
                throw new ArgumentOutOfRangeException(nameof(remainingRequiredProductCount));
            if (projectedDeficitProductCount < 0 ||
                projectedDeficitProductCount > remainingRequiredProductCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(projectedDeficitProductCount));
            }
            if (cartPackageCount < 0)
                throw new ArgumentOutOfRangeException(nameof(cartPackageCount));

            Index = index;
            ProductType = productType;
            PackageProductCount = packageProductCount;
            PackageCost = packageCost;
            StockProductCount = stockProductCount;
            InTransitProductCount = inTransitProductCount;
            MinimumRequiredProductCount = minimumRequiredProductCount;
            MaximumRequiredProductCount = maximumRequiredProductCount;
            RemainingRequiredProductCount = remainingRequiredProductCount;
            ProjectedDeficitProductCount = projectedDeficitProductCount;
            CartPackageCount = cartPackageCount;
            CartProductCount = checked(cartPackageCount * packageProductCount);
        }

        public int Index { get; }
        public ProductTypeId ProductType { get; }
        public int PackageProductCount { get; }
        public int PackageCost { get; }
        public int StockProductCount { get; }
        public int InTransitProductCount { get; }
        public int MinimumRequiredProductCount { get; }
        public int MaximumRequiredProductCount { get; }
        public int RemainingRequiredProductCount { get; }
        public int ProjectedDeficitProductCount { get; }
        public int CartPackageCount { get; }
        public int CartProductCount { get; }
    }
}
