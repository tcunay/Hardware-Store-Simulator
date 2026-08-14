using System;
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
                if (orderCounter == null)
                    throw new InvalidOperationException(
                        $"Consultation interaction targets missing entity " +
                        $"{request.TargetEntityId}.");
                if (!orderCounter.isOrderCounter)
                    continue;
                if (orderCounter.isDestructed || !orderCounter.isInteractable ||
                    !orderCounter.hasEntityId || !orderCounter.hasStoreEntityId)
                {
                    throw new InvalidOperationException(
                        "Order counter has incomplete consultation configuration.");
                }

                GameEntity player =
                    _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                if (player == null || !player.isPlayer || player.isDestructed ||
                    !player.hasEntityId)
                {
                    throw new InvalidOperationException(
                        $"Interaction source cannot use order counter " +
                        $"{orderCounter.EntityId}.");
                }
                if (player.isHandsOccupied ||
                    player.isModalOpen)
                {
                    continue;
                }
                if (!player.hasStoreEntityId || !player.hasMoveDirection)
                {
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} has incomplete consultation state.");
                }
                if (player.StoreEntityId != orderCounter.StoreEntityId)
                {
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} cannot consult at another store's counter.");
                }

                GameEntity visit =
                    _gameContext.GetEntityWithServingOrderCounterEntityId(
                        orderCounter.EntityId);
                if (visit == null)
                    continue;
                ValidateConsultingVisit(visit, orderCounter);
                if (player.hasConsultationVisitEntityId ||
                    player.hasProcurementTerminalEntityId ||
                    player.hasDayReportStoreEntityId)
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

        private static void ValidateConsultingVisit(
            GameEntity visit,
            GameEntity orderCounter)
        {
            if (visit.isDestructed || !visit.isCustomerVisit ||
                !visit.isCustomerVehicle || !visit.isCustomerVisitConsulting ||
                visit.isOrder || !visit.hasEntityId ||
                !visit.hasCustomerVisitStoreEntityId ||
                !visit.hasCustomerProjectType ||
                !visit.hasServingOrderCounterEntityId ||
                visit.CustomerVisitStoreEntityId != orderCounter.StoreEntityId ||
                visit.ServingOrderCounterEntityId != orderCounter.EntityId)
            {
                throw new InvalidOperationException(
                    $"Order counter {orderCounter.EntityId} serves invalid customer visit.");
            }

            int lifecycleCount =
                (visit.isCustomerVisitArriving ? 1 : 0) +
                (visit.isCustomerVisitQueued ? 1 : 0) +
                (visit.isCustomerVisitConsulting ? 1 : 0) +
                (visit.isCustomerVisitWaitingForLoadingBay ? 1 : 0) +
                (visit.isCustomerVisitMovingToLoadingBay ? 1 : 0) +
                (visit.isCustomerVisitLoading ? 1 : 0) +
                (visit.isCustomerVisitCompleted ? 1 : 0) +
                (visit.isCustomerVisitReturning ? 1 : 0) +
                (visit.isCustomerVisitDeparting ? 1 : 0) +
                (visit.isCustomerVisitAbandoning ? 1 : 0) +
                (visit.isCustomerVisitWaitingForAbandonDeparture ? 1 : 0) +
                (visit.isCustomerVisitAbandonDeparting ? 1 : 0);
            if (lifecycleCount != 1)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} must have exactly one lifecycle marker.");
            }
        }
    }
}
