using System;
using System.Collections.Generic;
using HardwareStore.Gameplay.Common.Economy;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct ProcurementSnapshot
    {
        public ProcurementSnapshot(
            ProcurementDemandKind demandKind,
            CustomerProjectTypeId projectType,
            int money,
            int freeStorageSlotCount,
            int selectedProductIndex,
            ProcurementProductSnapshot[] products,
            ProcurementCartSnapshot cart)
        {
            if (!Enum.IsDefined(typeof(ProcurementDemandKind), demandKind))
                throw new ArgumentOutOfRangeException(nameof(demandKind));
            if (!Enum.IsDefined(typeof(CustomerProjectTypeId), projectType))
                throw new ArgumentOutOfRangeException(nameof(projectType));
            if (money < 0)
                throw new ArgumentOutOfRangeException(nameof(money));
            if (freeStorageSlotCount < 0)
                throw new ArgumentOutOfRangeException(nameof(freeStorageSlotCount));
            if (products == null)
                throw new ArgumentNullException(nameof(products));
            if (products.Length == 0)
                throw new ArgumentException(
                    "The procurement catalog must present at least one product.",
                    nameof(products));
            if (selectedProductIndex < 0 || selectedProductIndex >= products.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedProductIndex));

            for (int index = 0; index < products.Length; index++)
            {
                ProcurementProductSnapshot product = products[index];
                if (product.Index != index)
                {
                    throw new ArgumentException(
                        "Procurement product indices must be contiguous and ordered.",
                        nameof(products));
                }
                switch (demandKind)
                {
                    case ProcurementDemandKind.ProjectForecast:
                        if (product.RemainingRequiredProductCount != 0 ||
                            product.ProjectedDeficitProductCount != 0)
                        {
                            throw new ArgumentException(
                                "Forecast procurement cards cannot contain exact-order " +
                                "counts.",
                                nameof(products));
                        }
                        break;
                    case ProcurementDemandKind.ConfirmedOrder:
                    case ProcurementDemandKind.SelectedCustomerOrder:
                        if (product.MinimumRequiredProductCount !=
                            product.RemainingRequiredProductCount ||
                            product.MaximumRequiredProductCount !=
                            product.RemainingRequiredProductCount)
                        {
                            throw new ArgumentException(
                                "Exact-order procurement cards must expose one demand count.",
                                nameof(products));
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(demandKind), demandKind, null);
                }
                for (int previous = 0; previous < index; previous++)
                {
                    if (products[previous].ProductType == product.ProductType)
                    {
                        throw new ArgumentException(
                            "The procurement catalog cannot contain duplicate product types.",
                            nameof(products));
                    }
                }
            }

            ValidateCartProducts(products, cart);
            DemandKind = demandKind;
            ProjectType = projectType;
            Money = money;
            FreeStorageSlotCount = freeStorageSlotCount;
            SelectedProductIndex = selectedProductIndex;
            Products = Array.AsReadOnly((ProcurementProductSnapshot[])products.Clone());
            Cart = cart;
        }

        public ProcurementDemandKind DemandKind { get; }
        public CustomerProjectTypeId ProjectType { get; }
        public int Money { get; }
        public int FreeStorageSlotCount { get; }
        public int SelectedProductIndex { get; }
        public IReadOnlyList<ProcurementProductSnapshot> Products { get; }
        public ProcurementCartSnapshot Cart { get; }

        private static void ValidateCartProducts(
            ProcurementProductSnapshot[] products,
            ProcurementCartSnapshot cart)
        {
            int matchedLineCount = 0;
            for (int productIndex = 0; productIndex < products.Length; productIndex++)
            {
                ProcurementProductSnapshot product = products[productIndex];
                ProcurementCartLineSnapshot? matchingLine = null;
                for (int lineIndex = 0; lineIndex < cart.Lines.Count; lineIndex++)
                {
                    ProcurementCartLineSnapshot line = cart.Lines[lineIndex];
                    if (line.ProductType != product.ProductType)
                        continue;

                    matchingLine = line;
                    matchedLineCount++;
                    break;
                }

                if (!matchingLine.HasValue)
                {
                    if (product.CartPackageCount != 0)
                    {
                        throw new ArgumentException(
                            "A product card references packages missing from the cart.",
                            nameof(products));
                    }
                    continue;
                }

                ProcurementCartLineSnapshot cartLine = matchingLine.Value;
                if (product.CartPackageCount != cartLine.PackageCount ||
                    product.PackageProductCount != cartLine.PackageProductCount ||
                    product.PackageCost != cartLine.PackageCost ||
                    product.CartProductCount != cartLine.ProductCount)
                {
                    throw new ArgumentException(
                        "Product card cart totals disagree with the matching cart line.",
                        nameof(products));
                }
            }

            if (matchedLineCount != cart.Lines.Count)
            {
                throw new ArgumentException(
                    "The cart contains a product absent from the procurement catalog.",
                    nameof(cart));
            }
        }
    }
}
