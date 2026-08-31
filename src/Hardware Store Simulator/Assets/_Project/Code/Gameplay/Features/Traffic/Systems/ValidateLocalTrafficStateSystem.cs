using System;
using Entitas;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Features.Traffic.Systems
{
    public sealed class ValidateLocalTrafficStateSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _participants;

        public ValidateLocalTrafficStateSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _participants = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.TrafficParticipant,
                    GameMatcher.EntityId,
                    GameMatcher.TrafficControlPolicy,
                    GameMatcher.TrafficPriority,
                    GameMatcher.TrafficDesiredVelocity,
                    GameMatcher.TrafficIntentDistance,
                    GameMatcher.TrafficAngularIntent,
                    GameMatcher.TrafficPreviousPosition)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity participant in _participants)
            {
                if (participant.TrafficPriority <= 0 ||
                    !IsFinite(participant.TrafficDesiredVelocity) ||
                    !IsFinite(participant.TrafficPreviousPosition) ||
                    !IsFinite(participant.TrafficIntentDistance) ||
                    participant.TrafficIntentDistance < 0f ||
                    !IsFinite(participant.TrafficAngularIntent))
                {
                    throw new InvalidOperationException(
                        $"Traffic participant {participant.EntityId} has invalid state.");
                }
                bool hasEntityConflict = participant.hasTrafficConflictEntityId;
                bool hasWorldConflict = participant.hasTrafficConflictCollider;
                if (participant.isTrafficYielding !=
                    (hasEntityConflict ^ hasWorldConflict))
                {
                    throw new InvalidOperationException(
                        $"Traffic participant {participant.EntityId} has an incomplete " +
                        "yield relation.");
                }
                if (!participant.isTrafficYielding)
                    continue;
                if (participant.TrafficControlPolicy is
                    TrafficControlPolicyId.Uncontrolled or
                    TrafficControlPolicyId.Coupled)
                {
                    throw new InvalidOperationException(
                        $"Uncontrolled traffic participant {participant.EntityId} cannot yield.");
                }

                if (hasWorldConflict)
                {
                    if (participant.TrafficConflictCollider == null)
                    {
                        throw new InvalidOperationException(
                            $"Traffic participant {participant.EntityId} yields to a " +
                            "missing world collider.");
                    }

                    continue;
                }

                GameEntity conflict = _gameContext.GetEntityWithEntityId(
                    participant.TrafficConflictEntityId);
                if (conflict == null || conflict.isDestructed ||
                    !conflict.isTrafficParticipant)
                {
                    throw new InvalidOperationException(
                        $"Traffic participant {participant.EntityId} yields to an invalid entity.");
                }
            }
        }

        private static bool IsFinite(UnityEngine.Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
