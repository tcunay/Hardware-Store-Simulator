using System;
using Entitas;
using HardwareStore.Gameplay.Common.Cursor;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Delivery.Systems
{
    public sealed class CloseProcurementAfterPurchaseSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly ICursorService _cursor;
        private readonly IGroup<GameEntity> _requests;

        public CloseProcurementAfterPurchaseSystem(GameContext gameContext,
            ICursorService cursor)
        {
            _gameContext = gameContext;
            _cursor = cursor;
            _requests = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.PurchaseDeliveryRequest,
                GameMatcher.PurchaseDeliverySucceeded,
                GameMatcher.SourceEntityId,
                GameMatcher.TargetEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity request in _requests)
            {
                GameEntity player = _gameContext.GetEntityWithEntityId(
                    request.SourceEntityId);
                if (!player.isPlayer || !player.hasMoveDirection ||
                    !player.isModalOpen ||
                    !player.hasProcurementTerminalEntityId ||
                    player.ProcurementTerminalEntityId != request.TargetEntityId)
                {
                    throw new InvalidOperationException(
                        $"Succeeded purchase request has invalid source player " +
                        $"{request.SourceEntityId}.");
                }

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
}
