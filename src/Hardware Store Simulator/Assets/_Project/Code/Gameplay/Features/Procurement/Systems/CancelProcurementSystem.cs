using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Cursor;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Procurement.Systems
{
    public sealed class CancelProcurementSystem : IExecuteSystem
    {
        private readonly ICursorService _cursor;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;
        private readonly List<GameEntity> _playerBuffer = new(1);

        public CancelProcurementSystem(GameContext gameContext,
            InputContext inputContext, ICursorService cursor)
        {
            _cursor = cursor;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.MoveDirection,
                GameMatcher.ModalOpen,
                GameMatcher.ProcurementTerminalEntityId));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(
                InputMatcher.InputState,
                InputMatcher.ToggleCursorPressed));
        }

        public void Execute()
        {
            foreach (InputEntity ignored in _inputs)
            foreach (GameEntity player in _players.GetEntities(_playerBuffer))
                Close(player);
        }

        private void Close(GameEntity player)
        {
            player.RemoveProcurementTerminalEntityId();
            player.isModalOpen = false;
            player.ReplaceMoveDirection(Vector3.zero);
            if (player.hasInteractionPrompt)
                player.RemoveInteractionPrompt();
            player.isFocusInteractionAvailable = false;
            player.isCursorLocked = true;
            _cursor.SetLocked(true);
        }
    }
}
