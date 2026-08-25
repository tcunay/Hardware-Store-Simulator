using Entitas;
using UnityEngine;

namespace HardwareStore.Gameplay.Components
{
    [Input] public class InputState : IComponent { }
    [Input] public class MoveInput : IComponent { public Vector2 Value; }
    [Input] public class LookInput : IComponent { public Vector2 Value; }
    [Input] public class SprintHeld : IComponent { }
    [Input] public class InteractPressed : IComponent { }
    [Input] public class ConfirmPressed : IComponent { }
    [Input] public class DropPressed : IComponent { }
    [Input] public class TrolleyPressed : IComponent { }
    [Input] public class PreviousPressed : IComponent { }
    [Input] public class NextPressed : IComponent { }
    [Input] public class IncreasePressed : IComponent { }
    [Input] public class DecreasePressed : IComponent { }
    [Input] public class ForkliftLiftInput : IComponent { public float Value; }
    [Input] public class ForkliftTransferPressed : IComponent { }
    [Input] public class ToggleCursorPressed : IComponent { }
    [Input] public class PointerLook : IComponent { }
}
