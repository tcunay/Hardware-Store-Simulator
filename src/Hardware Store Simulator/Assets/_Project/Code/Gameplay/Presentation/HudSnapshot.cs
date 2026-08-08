using System;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct HudSnapshot
    {
        public HudSnapshot(HudOrderState orderState, int loadedCount, int requiredCount, int money, string prompt,
            bool hasFocus, bool canInteract, bool hasItem, bool cursorLocked)
        {
            OrderState = orderState;
            LoadedCount = loadedCount;
            RequiredCount = requiredCount;
            Money = money;
            Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
            HasFocus = hasFocus;
            CanInteract = canInteract;
            HasItem = hasItem;
            CursorLocked = cursorLocked;
        }

        public HudOrderState OrderState { get; }
        public int LoadedCount { get; }
        public int RequiredCount { get; }
        public int Money { get; }
        public string Prompt { get; }
        public bool HasFocus { get; }
        public bool CanInteract { get; }
        public bool HasItem { get; }
        public bool CursorLocked { get; }
    }
}
