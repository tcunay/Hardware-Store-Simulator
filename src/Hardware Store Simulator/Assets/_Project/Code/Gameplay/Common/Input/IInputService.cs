using UnityEngine;

namespace HardwareStore.Gameplay.Common.Input
{
    public interface IInputService
    {
        Vector2 Move { get; }
        Vector2 Look { get; }
        bool SprintHeld { get; }
        bool InteractPressedThisFrame { get; }
        bool ConfirmPressedThisFrame { get; }
        bool DropPressedThisFrame { get; }
        bool TrolleyPressedThisFrame { get; }
        bool PreviousPressedThisFrame { get; }
        bool NextPressedThisFrame { get; }
        bool IncreasePressedThisFrame { get; }
        bool DecreasePressedThisFrame { get; }
        bool ToggleCursorPressedThisFrame { get; }
        bool LookUsesPointer { get; }
    }
}
