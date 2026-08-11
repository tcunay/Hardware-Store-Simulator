using System;
using System.Linq;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Procurement.Systems
{
    public sealed class ChangeProcurementSelectionSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly ProductTypeId[] _productTypes;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;

        public ChangeProcurementSelectionSystem(GameContext gameContext,
            InputContext inputContext, IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _productTypes = staticData.ProductTypes.ToArray();
            if (_productTypes.Length == 0)
                throw new InvalidOperationException("At least one product type is required.");

            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.EntityId,
                GameMatcher.ModalOpen,
                GameMatcher.ProcurementTerminalEntityId));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(InputMatcher.InputState)
                .AnyOf(InputMatcher.PreviousPressed, InputMatcher.NextPressed));
        }

        public void Execute()
        {
            foreach (InputEntity input in _inputs)
            {
                int offset = (input.isNextPressed ? 1 : 0) -
                             (input.isPreviousPressed ? 1 : 0);
                if (offset == 0)
                    continue;

                foreach (GameEntity player in _players)
                    ChangeSelection(player, offset);
            }
        }

        private void ChangeSelection(GameEntity player, int offset)
        {
            GameEntity terminal = _gameContext.GetEntityWithEntityId(
                player.ProcurementTerminalEntityId);
            if (!terminal.isProcurementTerminal || !terminal.hasSelectedProductType)
                throw new InvalidOperationException(
                    $"Player {player.EntityId} references an invalid procurement terminal " +
                    $"{terminal.EntityId}.");

            int currentIndex = Array.IndexOf(
                _productTypes,
                terminal.SelectedProductType);
            if (currentIndex < 0)
                throw new InvalidOperationException(
                    $"Product type {terminal.SelectedProductType} is not present in static data.");

            int selectedIndex =
                (currentIndex + offset + _productTypes.Length) % _productTypes.Length;
            terminal.ReplaceSelectedProductType(_productTypes[selectedIndex]);
        }
    }
}
