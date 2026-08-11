using System;
using System.Collections.Generic;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct ProcurementSnapshot
    {
        private const int ProductCardCount = 2;

        public ProcurementSnapshot(string projectTitle, int money,
            int freeStorageSlotCount, ProcurementProductSnapshot[] products)
        {
            ProjectTitle = projectTitle ??
                           throw new ArgumentNullException(nameof(projectTitle));
            if (money < 0)
                throw new ArgumentOutOfRangeException(nameof(money));
            if (freeStorageSlotCount < 0)
                throw new ArgumentOutOfRangeException(nameof(freeStorageSlotCount));
            if (products == null)
                throw new ArgumentNullException(nameof(products));
            if (products.Length != ProductCardCount)
            {
                throw new ArgumentException(
                    $"The procurement catalog must present exactly {ProductCardCount} products.",
                    nameof(products));
            }

            int selectedCount = 0;
            for (int index = 0; index < products.Length; index++)
            {
                if (products[index].Index != index)
                {
                    throw new ArgumentException(
                        "Procurement product indices must be contiguous and ordered.",
                        nameof(products));
                }
                if (products[index].Selected)
                    selectedCount++;
                for (int previous = 0; previous < index; previous++)
                {
                    if (products[previous].ProductType == products[index].ProductType)
                    {
                        throw new ArgumentException(
                            "The procurement catalog cannot contain duplicate product types.",
                            nameof(products));
                    }
                }
            }

            if (selectedCount != 1)
            {
                throw new ArgumentException(
                    "The procurement catalog must have exactly one selected product.",
                    nameof(products));
            }

            Money = money;
            FreeStorageSlotCount = freeStorageSlotCount;
            Products = Array.AsReadOnly((ProcurementProductSnapshot[])products.Clone());
        }

        public string ProjectTitle { get; }
        public int Money { get; }
        public int FreeStorageSlotCount { get; }
        public IReadOnlyList<ProcurementProductSnapshot> Products { get; }
    }
}
