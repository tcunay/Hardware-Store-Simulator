using Entitas;

namespace HardwareStore.Gameplay.Features.Cleanup.Systems
{
    public sealed class CleanupInputRequestsSystem : ICleanupSystem
    {
        private readonly IGroup<InputEntity> _inputs;

        public CleanupInputRequestsSystem(InputContext inputContext) =>
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(InputMatcher.InputState));

        public void Cleanup()
        {
            foreach (InputEntity input in _inputs)
            {
                input.isInteractPressed = false;
                input.isConfirmPressed = false;
                input.isDropPressed = false;
                input.isPreviousPressed = false;
                input.isNextPressed = false;
                input.isToggleCursorPressed = false;
            }
        }
    }
}
