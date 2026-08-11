using System;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct ProcurementProductSnapshot
    {
        public ProcurementProductSnapshot(int index, ProductTypeId productType,
            string productDisplayName, string productUnitLabel,
            int deliveryProductCount, int deliveryCost, int moneyAfterPurchase,
            int availableProductCount, int remainingRequiredProductCount,
            int deficitProductCount, bool purchaseAvailable,
            string purchaseStatus, bool selected)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (deliveryProductCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(deliveryProductCount));
            if (deliveryCost < 0)
                throw new ArgumentOutOfRangeException(nameof(deliveryCost));
            if (availableProductCount < 0)
                throw new ArgumentOutOfRangeException(nameof(availableProductCount));
            if (remainingRequiredProductCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(remainingRequiredProductCount));
            }
            if (deficitProductCount < 0 ||
                deficitProductCount > remainingRequiredProductCount)
            {
                throw new ArgumentOutOfRangeException(nameof(deficitProductCount));
            }

            Index = index;
            ProductType = productType;
            ProductDisplayName = productDisplayName ??
                                 throw new ArgumentNullException(nameof(productDisplayName));
            ProductUnitLabel = productUnitLabel ??
                               throw new ArgumentNullException(nameof(productUnitLabel));
            DeliveryProductCount = deliveryProductCount;
            DeliveryCost = deliveryCost;
            MoneyAfterPurchase = moneyAfterPurchase;
            AvailableProductCount = availableProductCount;
            RemainingRequiredProductCount = remainingRequiredProductCount;
            DeficitProductCount = deficitProductCount;
            PurchaseAvailable = purchaseAvailable;
            PurchaseStatus = purchaseStatus ??
                             throw new ArgumentNullException(nameof(purchaseStatus));
            Selected = selected;
        }

        public int Index { get; }
        public ProductTypeId ProductType { get; }
        public string ProductDisplayName { get; }
        public string ProductUnitLabel { get; }
        public int DeliveryProductCount { get; }
        public int DeliveryCost { get; }
        public int MoneyAfterPurchase { get; }
        public int AvailableProductCount { get; }
        public int RemainingRequiredProductCount { get; }
        public int DeficitProductCount { get; }
        public bool PurchaseAvailable { get; }
        public string PurchaseStatus { get; }
        public bool Selected { get; }
    }
}
