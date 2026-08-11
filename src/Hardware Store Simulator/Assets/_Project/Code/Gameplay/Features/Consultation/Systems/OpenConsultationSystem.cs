using Entitas;
using HardwareStore.Gameplay.Common.Cursor;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Consultation.Systems
{
    public sealed class OpenConsultationSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly ICursorService _cursor;
        private readonly IGroup<GameEntity> _requests;

        public OpenConsultationSystem(GameContext gameContext, ICursorService cursor)
        {
            _gameContext = gameContext;
            _cursor = cursor;
            _requests = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.InteractionRequest,
                GameMatcher.SourceEntityId,
                GameMatcher.TargetEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity request in _requests)
            {
                GameEntity orderCounter =
                    _gameContext.GetEntityWithEntityId(request.TargetEntityId);
                if (!orderCounter.isOrderCounter)
                    continue;

                GameEntity player =
                    _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                if (!player.isPlayer || player.isHandsOccupied ||
                    player.isModalOpen)
                {
                    continue;
                }
                if (!player.hasStoreEntityId || !player.hasMoveDirection ||
                    player.StoreEntityId != orderCounter.StoreEntityId)
                {
                    continue;
                }

                GameEntity visit =
                    _gameContext.GetEntityWithCustomerVisitStoreEntityId(
                        orderCounter.StoreEntityId);
                if (visit == null || !visit.isCustomerVisitConsulting)
                    continue;
                if (player.hasConsultationVisitEntityId ||
                    player.hasProcurementTerminalEntityId)
                {
                    throw new System.InvalidOperationException(
                        $"Player {player.EntityId} has a modal relation without ModalOpen.");
                }

                player.AddConsultationVisitEntityId(visit.EntityId);
                player.isModalOpen = true;
                player.ReplaceMoveDirection(Vector3.zero);
                if (player.hasInteractionPrompt)
                    player.RemoveInteractionPrompt();
                player.isFocusInteractionAvailable = false;
                if (player.hasFocusedInteractionType)
                    player.RemoveFocusedInteractionType();
                if (player.hasFocusedEntityId)
                    player.RemoveFocusedEntityId();
                orderCounter.isHighlighted = false;
                player.isCursorLocked = true;
                _cursor.SetLocked(true);
            }
        }
    }
}
