using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.Views;

namespace HardwareStore.Gameplay.Features.Presentation.Systems
{
    public sealed class PresentCustomerDissatisfactionSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly string _worldLabel;
        private readonly IGroup<GameEntity> _actors;
        private readonly IGroup<GameEntity> _missingViews;
        private readonly List<GameEntity> _actorBuffer = new(4);
        private readonly List<GameEntity> _missingViewBuffer = new(1);

        public PresentCustomerDissatisfactionSystem(
            GameContext gameContext,
            ILocalizationService localization)
        {
            _gameContext = gameContext;
            _worldLabel = localization.Resolve(
                LocalizationKey.WorldCustomerDissatisfied);
            if (string.IsNullOrWhiteSpace(_worldLabel))
                throw new InvalidOperationException(
                    "Customer dissatisfaction world label must not be blank.");

            _actors = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Customer,
                    GameMatcher.EntityId,
                    GameMatcher.View,
                    GameMatcher.CustomerActorVisitEntityId,
                    GameMatcher.CustomerDissatisfactionView)
                .NoneOf(GameMatcher.Destructed));
            _missingViews = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Customer,
                    GameMatcher.EntityId,
                    GameMatcher.View,
                    GameMatcher.CustomerActorVisitEntityId)
                .NoneOf(
                    GameMatcher.CustomerDissatisfactionView,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity actor in
                     _missingViews.GetEntities(_missingViewBuffer))
            {
                throw new InvalidOperationException(
                    $"Bound customer actor {actor.EntityId} has no dissatisfaction view.");
            }

            foreach (GameEntity actor in _actors.GetEntities(_actorBuffer))
                Present(actor);
        }

        private void Present(GameEntity actor)
        {
            CustomerDissatisfactionView view = actor.CustomerDissatisfactionView;
            if (view == null)
                throw new InvalidOperationException(
                    $"Customer actor {actor.EntityId} has a missing dissatisfaction view.");

            GameEntity visit = _gameContext.GetEntityWithEntityId(
                actor.CustomerActorVisitEntityId);
            if (visit == null || visit.isDestructed || !visit.isCustomerVisit ||
                !visit.isCustomerVehicle || !visit.hasEntityId ||
                !visit.hasCustomerVisitStoreEntityId ||
                _gameContext.GetEntityWithCustomerActorVisitEntityId(
                    visit.EntityId) != actor)
            {
                throw new InvalidOperationException(
                    $"Customer actor {actor.EntityId} has an invalid visit relation.");
            }

            bool waiting =
                visit.isCustomerVisitQueued || visit.isCustomerVisitConsulting;
            bool accepted = visit.isCustomerVisitReturning;
            bool abandoning = visit.isCustomerVisitAbandoning;
            int lifecycleCount =
                (visit.isCustomerVisitQueued ? 1 : 0) +
                (visit.isCustomerVisitConsulting ? 1 : 0) +
                (accepted ? 1 : 0) +
                (abandoning ? 1 : 0);
            if (lifecycleCount != 1)
                throw new InvalidOperationException(
                    $"Customer actor {actor.EntityId} has an invalid presented visit lifecycle.");

            if (waiting)
                ValidateWaitingVisit(visit);
            else if (accepted)
                ValidateAcceptedVisit(visit);
            else
                ValidateAbandoningVisit(visit);

            bool dissatisfied =
                (waiting && visit.isCustomerPatienceWarningIssued) || abandoning;
            view.SetDissatisfied(dissatisfied, _worldLabel);
        }

        private static void ValidateWaitingVisit(GameEntity visit)
        {
            if (visit.isOrder || !visit.hasCustomerPatienceRemaining)
                throw new InvalidOperationException(
                    $"Waiting customer visit {visit.EntityId} has invalid patience state.");

            float remaining = visit.CustomerPatienceRemaining;
            if (float.IsNaN(remaining) || float.IsInfinity(remaining) || remaining <= 0f)
                throw new InvalidOperationException(
                    $"Waiting customer visit {visit.EntityId} has invalid patience " +
                    $"{remaining}.");
        }

        private static void ValidateAcceptedVisit(GameEntity visit)
        {
            if (!visit.isOrder || visit.hasCustomerPatienceRemaining ||
                visit.isCustomerPatienceWarningIssued)
            {
                throw new InvalidOperationException(
                    $"Accepted customer visit {visit.EntityId} retains dissatisfaction state.");
            }
        }

        private static void ValidateAbandoningVisit(GameEntity visit)
        {
            if (visit.isOrder || visit.hasCustomerPatienceRemaining ||
                visit.isCustomerPatienceWarningIssued)
            {
                throw new InvalidOperationException(
                    $"Abandoning customer visit {visit.EntityId} has invalid patience state.");
            }
        }
    }
}
