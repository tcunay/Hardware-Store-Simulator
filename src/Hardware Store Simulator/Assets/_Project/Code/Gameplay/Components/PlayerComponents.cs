using Entitas;
using UnityEngine;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class Player : IComponent { }
    [Game] public class CharacterControllerComponent : IComponent { public CharacterController Value; }
    [Game] public class ViewPivotComponent : IComponent { public Transform Value; }
    [Game] public class CarryAnchorComponent : IComponent { public Transform Value; }
    [Game] public class DropOriginComponent : IComponent { public Transform Value; }
    [Game] public class WalkSpeed : IComponent { public float Value; }
    [Game] public class SprintSpeed : IComponent { public float Value; }
    [Game] public class CarryingSpeed : IComponent { public float Value; }
    [Game] public class Gravity : IComponent { public float Value; }
    [Game] public class VerticalVelocity : IComponent { public float Value; }
    [Game] public class HorizontalSpeed : IComponent { public float Value; }
    [Game] public class MoveDirection : IComponent { public Vector3 Value; }
    [Game] public class MovementSpeed : IComponent { public float Value; }
    [Game] public class ViewPitch : IComponent { public float Value; }
    [Game] public class MouseSensitivity : IComponent { public float Value; }
    [Game] public class GamepadLookSpeed : IComponent { public float Value; }
    [Game] public class MaxPitch : IComponent { public float Value; }
    [Game] public class HandsOccupied : IComponent { }
    [Game] public class CursorLocked : IComponent { }
}
