using System;
using Entitas;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Procurement.Systems
{
    public sealed class ChangeProcurementCartQuantitySystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IProcurementCartFactory _carts;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;

        public ChangeProcurementCartQuantitySystem(GameContext gameContext,
            InputContext inputContext, IProcurementCartFactory carts)
        {
            _gameContext = gameContext;
            _carts = carts;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.EntityId,
                GameMatcher.StoreEntityId,
                GameMatcher.ModalOpen,
                GameMatcher.ProcurementTerminalEntityId));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(InputMatcher.InputState)
                .AnyOf(InputMatcher.IncreasePressed, InputMatcher.DecreasePressed));
        }

        public void Execute()
        {
            foreach (InputEntity input in _inputs)
            {
                int offset = (input.isIncreasePressed ? 1 : 0) -
                             (input.isDecreasePressed ? 1 : 0);
                if (offset == 0)
                    continue;

                foreach (GameEntity player in _players)
                    ChangeQuantity(player, offset);
            }
        }

        private void ChangeQuantity(GameEntity player, int offset)
        {
            GameEntity terminal = _gameContext.GetEntityWithEntityId(
                player.ProcurementTerminalEntityId);
            if (terminal == null || !terminal.isProcurementTerminal ||
                !terminal.hasEntityId || !terminal.hasStoreEntityId ||
                terminal.StoreEntityId != player.StoreEntityId ||
                !terminal.hasSelectedProductType)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} references an invalid procurement terminal.");
            }

            GameEntity cart =
                _gameContext.GetEntityWithProcurementCartTerminalEntityId(
                    terminal.EntityId);
            if (cart == null || !cart.isProcurementCart || cart.isDestructed ||
                !cart.hasEntityId || !cart.hasStoreEntityId ||
                cart.StoreEntityId != player.StoreEntityId ||
                !cart.hasProcurementCartPackageCapacity ||
                cart.ProcurementCartPackageCapacity <= 0 ||
                cart.ProcurementCartPackageCapacity >
                ProcurementCartFactory.CurrentDeliveryPackageCapacity)
            {
                throw new InvalidOperationException(
                    $"Terminal {terminal.EntityId} has no valid procurement cart.");
            }

            GameEntity selectedLine = null;
            int totalPackageCount = 0;
            foreach (GameEntity line in _gameContext.GetEntitiesWithProcurementCartEntityId(
                         cart.EntityId))
            {
                if (line.isDestructed)
                    continue;
                if (!line.isProcurementCartLine || !line.hasEntityId ||
                    !line.hasProductType || !line.hasProcurementPackageCount ||
                    line.ProcurementPackageCount <= 0)
                {
                    throw new InvalidOperationException(
                        $"Cart {cart.EntityId} contains an invalid line.");
                }

                totalPackageCount = checked(
                    totalPackageCount + line.ProcurementPackageCount);
                if (line.ProductType != terminal.SelectedProductType)
                    continue;
                if (selectedLine != null)
                {
                    throw new InvalidOperationException(
                        $"Cart {cart.EntityId} contains duplicate " +
                        $"{terminal.SelectedProductType} lines.");
                }
                selectedLine = line;
            }
            if (totalPackageCount > cart.ProcurementCartPackageCapacity)
            {
                throw new InvalidOperationException(
                    $"Cart {cart.EntityId} exceeds package capacity.");
            }

            if (offset > 0)
            {
                if (totalPackageCount == cart.ProcurementCartPackageCapacity)
                    return;
                if (selectedLine == null)
                {
                    _carts.CreateLine(
                        cart.EntityId,
                        terminal.SelectedProductType,
                        packageCount: 1);
                    return;
                }

                selectedLine.ReplaceProcurementPackageCount(checked(
                    selectedLine.ProcurementPackageCount + 1));
                return;
            }

            if (selectedLine == null)
                return;
            if (selectedLine.ProcurementPackageCount == 1)
            {
                selectedLine.isDestructed = true;
                return;
            }

            selectedLine.ReplaceProcurementPackageCount(
                selectedLine.ProcurementPackageCount - 1);
        }
    }
}
