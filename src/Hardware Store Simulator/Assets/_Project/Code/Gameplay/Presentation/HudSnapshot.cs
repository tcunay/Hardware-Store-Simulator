using System;
using System.Collections.Generic;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct HudSnapshot
    {
        public HudSnapshot(DayClockSnapshot dayClock, HudOrderState orderState,
            CustomerProjectTypeId? projectType,
            OrderLineSnapshot[] orderLines, int totalAvailableProductCount,
            int totalLoadedProductCount, int totalRequiredProductCount,
            int money, int stockCount, bool hasActiveDelivery,
            ProductTypeId deliveryProductType, int deliveryStockedCount,
            int deliveryProductCount, ProductTypeId? carriedProductType,
            LocalizedText prompt, bool hasFocus, bool canInteract, bool hasItem,
            bool isPushingTrolley, bool cursorLocked)
        {
            DayClock = dayClock;
            OrderState = orderState;
            ProjectType = projectType;
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
            DeliveryStockedCount = deliveryStockedCount;
            DeliveryProductCount = deliveryProductCount;
            CarriedProductType = carriedProductType;
            Prompt = prompt;
            HasFocus = hasFocus;
            CanInteract = canInteract;
            HasItem = hasItem;
            IsPushingTrolley = isPushingTrolley;
            CursorLocked = cursorLocked;
        }

        public DayClockSnapshot DayClock { get; }
        public HudOrderState OrderState { get; }
        public CustomerProjectTypeId? ProjectType { get; }
        public IReadOnlyList<OrderLineSnapshot> OrderLines { get; }
        public int TotalAvailableProductCount { get; }
        public int TotalLoadedProductCount { get; }
        public int TotalRequiredProductCount { get; }
        public int Money { get; }
        public int StockCount { get; }
        public bool HasActiveDelivery { get; }
        public ProductTypeId DeliveryProductType { get; }
        public int DeliveryStockedCount { get; }
        public int DeliveryProductCount { get; }
        public ProductTypeId? CarriedProductType { get; }
        public LocalizedText Prompt { get; }
        public bool HasFocus { get; }
        public bool CanInteract { get; }
        public bool HasItem { get; }
        public bool IsPushingTrolley { get; }
        public bool CursorLocked { get; }
    }
}
