using Entitas;
using HardwareStore.Gameplay.Common.Input;

namespace HardwareStore.Gameplay.Features.Input.Systems
{
    public sealed class EmitInputSystem : IExecuteSystem
    {
        private readonly IInputService _inputService;
        private readonly IGroup<InputEntity> _inputs;

        public EmitInputSystem(InputContext inputContext, IInputService inputService)
        {
            _inputService = inputService;
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(InputMatcher.InputState));
        }

        public void Execute()
        {
            foreach (InputEntity input in _inputs)
            {
                input.ReplaceMoveInput(_inputService.Move);
                input.ReplaceLookInput(_inputService.Look);
                input.isSprintHeld = _inputService.SprintHeld;
                input.isInteractPressed = _inputService.InteractPressedThisFrame;
                input.isConfirmPressed = _inputService.ConfirmPressedThisFrame;
                input.isDropPressed = _inputService.DropPressedThisFrame;
                input.isTrolleyPressed = _inputService.TrolleyPressedThisFrame;
                input.isPreviousPressed = _inputService.PreviousPressedThisFrame;
                input.isNextPressed = _inputService.NextPressedThisFrame;
                input.isIncreasePressed = _inputService.IncreasePressedThisFrame;
                input.isDecreasePressed = _inputService.DecreasePressedThisFrame;
                input.ReplaceForkliftLiftInput(_inputService.ForkliftLiftInput);
                input.isForkliftTransferPressed = _inputService.ForkliftTransferPressed;
                input.isToggleCursorPressed = _inputService.ToggleCursorPressedThisFrame;
                input.isPointerLook = _inputService.LookUsesPointer;
            }
        }
    }
}
