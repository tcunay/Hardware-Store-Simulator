using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Factories;

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

            GameEntity cart =
                _gameContext.GetEntityWithProcurementCartTerminalEntityId(
                    terminal.EntityId);
            if (cart == null)
            {
                throw new System.InvalidOperationException(
                    $"Terminal {terminal.EntityId} has no procurement cart.");
            }
            if (!cart.isProcurementCart || cart.isDestructed ||
                !cart.hasEntityId || !cart.hasStoreEntityId ||
                cart.StoreEntityId != player.StoreEntityId ||
                !cart.hasProcurementCartPackageCapacity ||
                cart.ProcurementCartPackageCapacity <= 0 ||
                cart.ProcurementCartPackageCapacity >
                ProcurementCartFactory.CurrentDeliveryPackageCapacity)
            {
                throw new System.InvalidOperationException(
                    $"Terminal {terminal.EntityId} owns an invalid procurement cart.");
            }

            bool hasLine = false;
            foreach (GameEntity line in
                     _gameContext.GetEntitiesWithProcurementCartEntityId(cart.EntityId))
            {
                if (line.isDestructed)
                    continue;
                if (!line.isProcurementCartLine || !line.hasEntityId ||
                    !line.hasProductType || !line.hasProcurementPackageCount ||
                    line.ProcurementPackageCount <= 0)
                {
                    throw new System.InvalidOperationException(
                        $"Cart {cart.EntityId} contains an invalid line.");
                }
                hasLine = true;
            }
            if (!hasLine)
                return;

            CreateEntity.Empty()
                .AddSourceEntityId(player.EntityId)
                .AddTargetEntityId(terminal.EntityId)
                .isPurchaseDeliveryRequest = true;
        }
    }
}
