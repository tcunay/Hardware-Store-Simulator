using UnityEngine;

namespace HardwareStore.Gameplay.Common.Input
{
    public interface IInputService
    {
        Vector2 Move { get; }
        Vector2 Look { get; }
        bool SprintHeld { get; }
        bool InteractPressedThisFrame { get; }
        bool DropPressedThisFrame { get; }
        bool ToggleCursorPressedThisFrame { get; }
        bool LookUsesPointer { get; }
    }
}
