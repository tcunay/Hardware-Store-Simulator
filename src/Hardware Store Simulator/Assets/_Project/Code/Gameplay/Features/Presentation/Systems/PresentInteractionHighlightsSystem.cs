using Entitas;

namespace HardwareStore.Gameplay.Features.Presentation.Systems
{
    public sealed class PresentInteractionHighlightsSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _views;

        public PresentInteractionHighlightsSystem(GameContext gameContext) =>
            _views = gameContext.GetGroup(GameMatcher.AllOf(GameMatcher.InteractionView));

        public void Execute()
        {
            foreach (GameEntity entity in _views)
                entity.InteractionView.SetHighlighted(entity.isHighlighted);
        }
    }
}
