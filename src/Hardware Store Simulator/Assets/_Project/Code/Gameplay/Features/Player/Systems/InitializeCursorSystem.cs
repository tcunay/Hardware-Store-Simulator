using Entitas;
using HardwareStore.Gameplay.Common.Cursor;

namespace HardwareStore.Gameplay.Features.Player.Systems
{
    public sealed class InitializeCursorSystem : IInitializeSystem
    {
        private readonly ICursorService _cursor;
        private readonly IGroup<GameEntity> _players;

        public InitializeCursorSystem(GameContext gameContext, ICursorService cursor)
        {
            _cursor = cursor;
            _players = gameContext.GetGroup(GameMatcher.AllOf(GameMatcher.Player));
        }

        public void Initialize()
        {
            _cursor.SetLocked(true);

            foreach (GameEntity player in _players)
                player.isCursorLocked = true;
        }
    }
}
