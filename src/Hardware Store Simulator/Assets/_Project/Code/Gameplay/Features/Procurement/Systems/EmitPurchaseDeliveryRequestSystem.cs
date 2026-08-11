using Entitas;
using HardwareStore.Common.Entity;

namespace HardwareStore.Gameplay.Features.Procurement.Systems
{
    public sealed class EmitPurchaseDeliveryRequestSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;

        public EmitPurchaseDeliveryRequestSystem(GameContext gameContext,
            InputContext inputContext)
        {
            _gameContext = gameContext;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.EntityId,
                GameMatcher.StoreEntityId,
                GameMatcher.ModalOpen,
                GameMatcher.ProcurementTerminalEntityId));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(
                InputMatcher.InputState,
                InputMatcher.ConfirmPressed));
        }

        public void Execute()
        {
            foreach (InputEntity input in _inputs)
            {
                if (input.isToggleCursorPressed)
                    continue;

                foreach (GameEntity player in _players)
                    EmitRequest(player);
            }
        }

        private void EmitRequest(GameEntity player)
        {
            GameEntity terminal = _gameContext.GetEntityWithEntityId(
                player.ProcurementTerminalEntityId);
            if (!terminal.isProcurementTerminal ||
                terminal.StoreEntityId != player.StoreEntityId)
            {
                throw new System.InvalidOperationException(
                    $"Player {player.EntityId} references an invalid procurement terminal " +
                    $"{terminal.EntityId}.");
            }

            CreateEntity.Empty()
                .AddSourceEntityId(player.EntityId)
                .AddTargetEntityId(terminal.EntityId)
                .isPurchaseDeliveryRequest = true;
        }
    }
}
