using Entitas;

namespace HardwareStore.Gameplay.Features.Carrying.Systems
{
    public sealed class FollowHeldProductSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;

        public FollowHeldProductSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.HeldProductId,
                GameMatcher.CarryAnchor));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                GameEntity product = _gameContext.GetEntityWithEntityId(player.HeldProductId);
                product.ProductView.FollowHands(player.CarryAnchor);
            }
        }
    }
}
