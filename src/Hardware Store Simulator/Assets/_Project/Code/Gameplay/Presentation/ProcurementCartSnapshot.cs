using System;
using System.Collections.Generic;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct ProcurementCartSnapshot
    {
        public ProcurementCartSnapshot(
            ProcurementCartLineSnapshot[] lines,
            int packageCount,
            int packageCapacity,
            int productCount,
            int totalCost,
            int moneyAfterPurchase,
            int requiredStorageSlotCount,
            ProcurementPurchaseState purchaseState)
        {
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));
            if (packageCount < 0)
                throw new ArgumentOutOfRangeException(nameof(packageCount));
            if (packageCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(packageCapacity));
            if (packageCount > packageCapacity)
                throw new ArgumentOutOfRangeException(nameof(packageCount));
            if (productCount < 0)
                throw new ArgumentOutOfRangeException(nameof(productCount));
            if (totalCost < 0)
                throw new ArgumentOutOfRangeException(nameof(totalCost));
            if (requiredStorageSlotCount < 0)
                throw new ArgumentOutOfRangeException(nameof(requiredStorageSlotCount));
            if (!Enum.IsDefined(typeof(ProcurementPurchaseState), purchaseState))
                throw new ArgumentOutOfRangeException(nameof(purchaseState));

            int calculatedPackageCount = 0;
            int calculatedProductCount = 0;
            int calculatedCost = 0;
            for (int index = 0; index < lines.Length; index++)
            {
                ProcurementCartLineSnapshot line = lines[index];
                if (line.LineIndex != index)
                {
                    throw new ArgumentException(
                        "Procurement cart line indices must be contiguous and ordered.",
                        nameof(lines));
                }
                for (int previous = 0; previous < index; previous++)
                {
                    if (lines[previous].ProductType == line.ProductType)
                    {
                        throw new ArgumentException(
                            "Procurement cart cannot contain duplicate product types.",
                            nameof(lines));
                    }
                }

                calculatedPackageCount = checked(
                    calculatedPackageCount + line.PackageCount);
                calculatedProductCount = checked(
                    calculatedProductCount + line.ProductCount);
                calculatedCost = checked(calculatedCost + line.LineCost);
            }

            if (calculatedPackageCount != packageCount ||
                calculatedProductCount != productCount ||
                calculatedCost != totalCost)
            {
                throw new ArgumentException(
                    "Procurement cart totals must equal the sum of its immutable lines.",
                    nameof(lines));
            }
            if ((packageCount == 0) != (lines.Length == 0))
            {
                throw new ArgumentException(
                    "An empty procurement cart cannot contain lines or package totals.",
                    nameof(lines));
            }
            if (packageCount == 0 && (productCount != 0 || totalCost != 0))
            {
                throw new ArgumentException(
                    "An empty procurement cart must expose zero product and cost totals.",
                    nameof(lines));
            }
            if (packageCount > 0 && (productCount <= 0 || totalCost <= 0))
            {
                throw new ArgumentException(
                    "A non-empty procurement cart must expose positive product and cost totals.",
                    nameof(lines));
            }
            if (requiredStorageSlotCount != productCount)
            {
                throw new ArgumentException(
                    "The current unit-storage model requires one storage slot per product.",
                    nameof(requiredStorageSlotCount));
            }

            Lines = Array.AsReadOnly((ProcurementCartLineSnapshot[])lines.Clone());
            PackageCount = packageCount;
            PackageCapacity = packageCapacity;
            ProductCount = productCount;
            TotalCost = totalCost;
            MoneyAfterPurchase = moneyAfterPurchase;
            RequiredStorageSlotCount = requiredStorageSlotCount;
            PurchaseState = purchaseState;
        }

        public IReadOnlyList<ProcurementCartLineSnapshot> Lines { get; }
        public int PackageCount { get; }
        public int PackageCapacity { get; }
        public int ProductCount { get; }
        public int TotalCost { get; }
        public int MoneyAfterPurchase { get; }
        public int RequiredStorageSlotCount { get; }
        public ProcurementPurchaseState PurchaseState { get; }
        public bool CanCheckout =>
            PackageCount > 0 && PurchaseState == ProcurementPurchaseState.Available;
    }
}
