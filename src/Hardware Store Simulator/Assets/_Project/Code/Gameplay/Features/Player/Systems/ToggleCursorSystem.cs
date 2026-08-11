using Entitas;
using HardwareStore.Gameplay.Common.Cursor;

namespace HardwareStore.Gameplay.Features.Player.Systems
{
    public sealed class ToggleCursorSystem : IExecuteSystem
    {
        private readonly ICursorService _cursor;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;

        public ToggleCursorSystem(GameContext gameContext, InputContext inputContext, ICursorService cursor)
        {
            _cursor = cursor;
            _players = gameContext.GetGroup(GameMatcher.AllOf(GameMatcher.Player)
                .NoneOf(GameMatcher.ModalOpen));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(
                InputMatcher.InputState,
                InputMatcher.ToggleCursorPressed));
        }

        public void Execute()
        {
            foreach (InputEntity ignored in _inputs)
            {
                bool locked = !_cursor.IsLocked;
                _cursor.SetLocked(locked);

                foreach (GameEntity player in _players)
                    player.isCursorLocked = locked;
            }
        }
    }
}
