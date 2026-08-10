using System;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct HudSnapshot
    {
        public HudSnapshot(HudOrderState orderState,
            ProductTypeId requiredProductType, string requiredProductDisplayName,
            string requiredProductUnitLabel, int availableProductCount,
            int loadedCount, int requiredCount, int money, int stockCount,
            bool hasActiveDelivery, ProductTypeId deliveryProductType,
            string deliveryProductDisplayName, string deliveryProductUnitLabel,
            int deliveryStockedCount, int deliveryProductCount,
            string carriedProductDisplayName, string prompt,
            bool hasFocus, bool canInteract, bool hasItem, bool cursorLocked)
        {
            OrderState = orderState;
            RequiredProductType = requiredProductType;
            RequiredProductDisplayName = requiredProductDisplayName ??
                                         throw new ArgumentNullException(
                                             nameof(requiredProductDisplayName));
            RequiredProductUnitLabel = requiredProductUnitLabel ??
                                       throw new ArgumentNullException(
                                           nameof(requiredProductUnitLabel));
            AvailableProductCount = availableProductCount;
            LoadedCount = loadedCount;
            RequiredCount = requiredCount;
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
        public ProductTypeId RequiredProductType { get; }
        public string RequiredProductDisplayName { get; }
        public string RequiredProductUnitLabel { get; }
        public int AvailableProductCount { get; }
        public int LoadedCount { get; }
        public int RequiredCount { get; }
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
