using Entitas;
using HardwareStore.Common.Entity;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class EmitInteractionRequestSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;

        public EmitInteractionRequestSystem(GameContext gameContext, InputContext inputContext)
        {
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.EntityId,
                GameMatcher.FocusedEntityId,
                GameMatcher.FocusInteractionAvailable)
                .NoneOf(GameMatcher.ModalOpen));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(
                InputMatcher.InputState,
                InputMatcher.InteractPressed));
        }

        public void Execute()
        {
            foreach (InputEntity ignored in _inputs)
            foreach (GameEntity player in _players)
            {
                GameEntity request = CreateEntity.Empty();
                request.isInteractionRequest = true;
                request.AddSourceEntityId(player.EntityId);
                request.AddTargetEntityId(player.FocusedEntityId);
            }
        }
    }
}
