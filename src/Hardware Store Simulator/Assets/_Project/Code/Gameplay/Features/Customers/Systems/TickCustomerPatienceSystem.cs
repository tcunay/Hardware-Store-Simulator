using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Time;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class TickCustomerPatienceSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly ITimeService _time;
        private readonly IGameEventFactory _events;
        private readonly float _warningThreshold;
        private readonly IGroup<GameEntity> _visits;
        private readonly IGroup<GameEntity> _consultationPlayers;
        private readonly List<GameEntity> _visitBuffer = new(4);
        private readonly List<GameEntity> _playerBuffer = new(1);

        public TickCustomerPatienceSystem(GameContext gameContext,
            ITimeService time, IStaticDataService staticData,
            IGameEventFactory events)
        {
            _gameContext = gameContext;
            _time = time;
            _events = events;
            CustomerFlowConfig config = staticData.CustomerFlow;
            _warningThreshold = config.PatienceWarningThreshold;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.EntityId,
                    GameMatcher.CustomerVisitStoreEntityId,
                    GameMatcher.CustomerPatienceRemaining)
                .AnyOf(
                    GameMatcher.CustomerVisitQueued,
                    GameMatcher.CustomerVisitConsulting)
                .NoneOf(
                    GameMatcher.Order,
                    GameMatcher.Destructed));
            _consultationPlayers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Player,
                    GameMatcher.EntityId,
                    GameMatcher.ModalOpen,
                    GameMatcher.ConsultationVisitEntityId)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            float deltaTime = _time.DeltaTime;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new InvalidOperationException(
                    $"Customer patience cannot tick with delta time {deltaTime}.");

            _consultationPlayers.GetEntities(_playerBuffer);
            foreach (GameEntity visit in _visits.GetEntities(_visitBuffer))
                Tick(visit, deltaTime);
        }

        private void Tick(GameEntity visit, float deltaTime)
        {
            if (visit.isCustomerVisitQueued == visit.isCustomerVisitConsulting)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has ambiguous patience lifecycle.");

            GameEntity store = _gameContext.GetEntityWithEntityId(
                visit.CustomerVisitStoreEntityId);
            if (store == null || store.isDestructed || !store.isStore ||
                !store.hasEntityId || (!store.isStoreOpen && !store.isStoreClosing) ||
                store.isStoreOpen == store.isStoreClosing)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} cannot tick patience for an inactive store.");
            }

            float previous = visit.CustomerPatienceRemaining;
            if (float.IsNaN(previous) || float.IsInfinity(previous) || previous < 0f)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has invalid patience {previous}.");
            if (HasActiveConsultationModal(visit))
                return;

            float remaining = Math.Max(0f, previous - deltaTime);
            visit.ReplaceCustomerPatienceRemaining(remaining);
            if (remaining <= 0f || previous <= _warningThreshold ||
                remaining > _warningThreshold)
            {
                return;
            }
            if (visit.isCustomerPatienceWarningIssued)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} emitted its patience warning twice.");

            visit.isCustomerPatienceWarningIssued = true;
            _events.EmitCustomerPatienceWarning(visit.EntityId);
        }

        private bool HasActiveConsultationModal(GameEntity visit)
        {
            GameEntity owner = null;
            foreach (GameEntity player in _playerBuffer)
            {
                if (player.ConsultationVisitEntityId != visit.EntityId)
                    continue;
                if (!visit.isCustomerVisitConsulting)
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} has a modal for non-consulting customer " +
                        $"visit {visit.EntityId}.");
                if (owner != null)
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has more than one consultation modal.");
                owner = player;
            }

            return owner != null;
        }
    }
}
