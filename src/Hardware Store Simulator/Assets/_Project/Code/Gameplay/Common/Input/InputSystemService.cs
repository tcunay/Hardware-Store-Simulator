using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace HardwareStore.Gameplay.Common.Input
{
    public sealed class InputSystemService : IInputService, IInitializable, IDisposable
    {
        private InputActionMap _playerMap;
        private InputAction _move;
        private InputAction _look;
        private InputAction _interact;
        private InputAction _confirm;
        private InputAction _drop;
        private InputAction _trolley;
        private InputAction _previous;
        private InputAction _next;
        private InputAction _increase;
        private InputAction _decrease;
        private InputAction _forkliftLift;
        private InputAction _forkliftTransfer;
        private InputAction _sprint;

        public Vector2 Move => _move.ReadValue<Vector2>();
        public Vector2 Look => _look.ReadValue<Vector2>();
        public bool SprintHeld => _sprint.IsPressed();
        public bool InteractPressedThisFrame => _interact.WasPressedThisFrame();
        public bool ConfirmPressedThisFrame => _confirm.WasPressedThisFrame();
        public bool DropPressedThisFrame => _drop.WasPressedThisFrame();
        public bool TrolleyPressedThisFrame => _trolley.WasPressedThisFrame();
        public bool PreviousPressedThisFrame => _previous.WasPressedThisFrame();
        public bool NextPressedThisFrame => _next.WasPressedThisFrame();
        public bool IncreasePressedThisFrame => _increase.WasPressedThisFrame();
        public bool DecreasePressedThisFrame => _decrease.WasPressedThisFrame();
        public float ForkliftLiftInput => _forkliftLift.ReadValue<float>();
        public bool ForkliftTransferPressed => _forkliftTransfer.WasPressedThisFrame();
        public bool ToggleCursorPressedThisFrame =>
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        public bool LookUsesPointer => _look.activeControl?.device is Pointer;

        public void Initialize()
        {
            _playerMap = InputSystem.actions.FindActionMap("Player", true);
            _move = _playerMap.FindAction("Move", true);
            _look = _playerMap.FindAction("Look", true);
            _interact = _playerMap.FindAction("Interact", true);
            _confirm = _playerMap.FindAction("Confirm", true);
            _drop = _playerMap.FindAction("Drop", true);
            _trolley = _playerMap.FindAction("Trolley", true);
            _previous = _playerMap.FindAction("Previous", true);
            _next = _playerMap.FindAction("Next", true);
            _increase = _playerMap.FindAction("Increase", true);
            _decrease = _playerMap.FindAction("Decrease", true);
            _forkliftLift = _playerMap.FindAction("ForkliftLift", true);
            _forkliftTransfer = _playerMap.FindAction("ForkliftTransfer", true);
            _sprint = _playerMap.FindAction("Sprint", true);
            _playerMap.Enable();
        }

        public void Dispose() => _playerMap.Disable();
    }
}
