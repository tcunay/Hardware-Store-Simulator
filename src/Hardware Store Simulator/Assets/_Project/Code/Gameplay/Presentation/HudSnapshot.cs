using System;
using System.Collections.Generic;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct HudSnapshot
    {
        public HudSnapshot(HudOrderState orderState, string projectTitle,
            OrderLineSnapshot[] orderLines, int totalAvailableProductCount,
            int totalLoadedProductCount, int totalRequiredProductCount,
            int money, int stockCount, bool hasActiveDelivery,
            ProductTypeId deliveryProductType, string deliveryProductDisplayName,
            string deliveryProductUnitLabel, int deliveryStockedCount,
            int deliveryProductCount, string carriedProductDisplayName,
            string prompt, bool hasFocus, bool canInteract, bool hasItem,
            bool cursorLocked)
        {
            OrderState = orderState;
            ProjectTitle = projectTitle ?? throw new ArgumentNullException(nameof(projectTitle));
            if (orderLines == null)
                throw new ArgumentNullException(nameof(orderLines));
            if (totalAvailableProductCount < 0)
                throw new ArgumentOutOfRangeException(nameof(totalAvailableProductCount));
            if (totalLoadedProductCount < 0)
                throw new ArgumentOutOfRangeException(nameof(totalLoadedProductCount));
            if (totalRequiredProductCount < 0)
                throw new ArgumentOutOfRangeException(nameof(totalRequiredProductCount));

            OrderLines = Array.AsReadOnly((OrderLineSnapshot[])orderLines.Clone());
            TotalAvailableProductCount = totalAvailableProductCount;
            TotalLoadedProductCount = totalLoadedProductCount;
            TotalRequiredProductCount = totalRequiredProductCount;
            Money = money;
            StockCount = stockCount;
            HasActiveDelivery = hasActiveDelivery;
            DeliveryProductType = deliveryProductType;
            DeliveryProductDisplayName = deliveryProductDisplayName ??
                                         throw new ArgumentNullException(
                                             nameof(deliveryProductDisplayName));
            DeliveryProductUnitLabel = deliveryProductUnitLabel ??
                                       throw new ArgumentNullException(
                                           nameof(deliveryProductUnitLabel));
            DeliveryStockedCount = deliveryStockedCount;
            DeliveryProductCount = deliveryProductCount;
            CarriedProductDisplayName = carriedProductDisplayName ??
                                        throw new ArgumentNullException(
                                            nameof(carriedProductDisplayName));
            Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
            HasFocus = hasFocus;
            CanInteract = canInteract;
            HasItem = hasItem;
            CursorLocked = cursorLocked;
        }

        public HudOrderState OrderState { get; }
        public string ProjectTitle { get; }
        public IReadOnlyList<OrderLineSnapshot> OrderLines { get; }
        public int TotalAvailableProductCount { get; }
        public int TotalLoadedProductCount { get; }
        public int TotalRequiredProductCount { get; }
        public int Money { get; }
        public int StockCount { get; }
        public bool HasActiveDelivery { get; }
        public ProductTypeId DeliveryProductType { get; }
        public string DeliveryProductDisplayName { get; }
        public string DeliveryProductUnitLabel { get; }
        public int DeliveryStockedCount { get; }
        public int DeliveryProductCount { get; }
        public string CarriedProductDisplayName { get; }
        public string Prompt { get; }
        public bool HasFocus { get; }
        public bool CanInteract { get; }
        public bool HasItem { get; }
        public bool CursorLocked { get; }
    }
}
