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
                    player.hasConsultationVisitEntityId)
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

                player.AddConsultationVisitEntityId(visit.EntityId);
                player.ReplaceMoveDirection(Vector3.zero);
                player.isCursorLocked = true;
                _cursor.SetLocked(true);
            }
        }
    }
}
