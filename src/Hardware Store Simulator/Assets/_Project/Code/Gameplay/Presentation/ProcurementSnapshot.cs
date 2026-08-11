using System;
using System.Collections.Generic;
using HardwareStore.Gameplay.Common.Economy;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct ProcurementSnapshot
    {
        private const int ProductCardCount = 2;

        public ProcurementSnapshot(
            ProcurementDemandKind demandKind,
            CustomerProjectTypeId projectType,
            int money,
            int freeStorageSlotCount,
            ProcurementProductSnapshot[] products)
        {
            if (!Enum.IsDefined(typeof(ProcurementDemandKind), demandKind))
                throw new ArgumentOutOfRangeException(nameof(demandKind));
            if (!Enum.IsDefined(typeof(CustomerProjectTypeId), projectType))
                throw new ArgumentOutOfRangeException(nameof(projectType));
            DemandKind = demandKind;
            ProjectType = projectType;
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
                if (demandKind == ProcurementDemandKind.ProjectForecast)
                {
                    if (products[index].RemainingRequiredProductCount != 0 ||
                        products[index].DeficitProductCount != 0)
                    {
                        throw new ArgumentException(
                            "Forecast procurement cards cannot contain confirmed-order counts.",
                            nameof(products));
                    }
                }
                else if (products[index].MinimumRequiredProductCount !=
                         products[index].RemainingRequiredProductCount ||
                         products[index].MaximumRequiredProductCount !=
                         products[index].RemainingRequiredProductCount)
                {
                    throw new ArgumentException(
                        "Confirmed-order procurement cards must expose one exact demand count.",
                        nameof(products));
                }
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

        public ProcurementDemandKind DemandKind { get; }
        public CustomerProjectTypeId ProjectType { get; }
        public int Money { get; }
        public int FreeStorageSlotCount { get; }
        public IReadOnlyList<ProcurementProductSnapshot> Products { get; }
    }
}
