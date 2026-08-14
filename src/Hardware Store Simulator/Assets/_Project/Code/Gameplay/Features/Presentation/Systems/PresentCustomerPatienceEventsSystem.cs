using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.Presentation;

namespace HardwareStore.Gameplay.Features.Presentation.Systems
{
    public sealed class PresentCustomerPatienceEventsSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly INotificationService _notifications;
        private readonly IGroup<GameEntity> _warningEvents;
        private readonly IGroup<GameEntity> _abandonedEvents;
        private readonly List<GameEntity> _warningBuffer = new(4);
        private readonly List<GameEntity> _abandonedBuffer = new(4);

        public PresentCustomerPatienceEventsSystem(
            GameContext gameContext,
            INotificationService notifications)
        {
            _gameContext = gameContext;
            _notifications = notifications;
            _warningEvents = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.CustomerPatienceWarningEvent,
                GameMatcher.CustomerEventVisitEntityId));
            _abandonedEvents = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.CustomerAbandonedEvent,
                GameMatcher.CustomerEventVisitEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity warningEvent in
                     _warningEvents.GetEntities(_warningBuffer))
            {
                PresentWarning(warningEvent);
                warningEvent.Destroy();
            }

            // A leave notification intentionally has priority when both event kinds occur
            // for different customers during the same frame.
            foreach (GameEntity abandonedEvent in
                     _abandonedEvents.GetEntities(_abandonedBuffer))
            {
                PresentAbandonment(abandonedEvent);
                abandonedEvent.Destroy();
            }
        }

        private void PresentWarning(GameEntity warningEvent)
        {
            ValidateEventKind(warningEvent, expectsWarning: true);
            GameEntity visit = ResolveVisit(warningEvent);
            if (visit.isOrder ||
                (!visit.isCustomerVisitQueued && !visit.isCustomerVisitConsulting) ||
                visit.isCustomerVisitQueued == visit.isCustomerVisitConsulting ||
                !visit.hasCustomerPatienceRemaining ||
                !visit.isCustomerPatienceWarningIssued)
            {
                throw new InvalidOperationException(
                    $"Patience warning event references invalid customer visit " +
                    $"{visit.EntityId}.");
            }

            float remaining = visit.CustomerPatienceRemaining;
            if (float.IsNaN(remaining) || float.IsInfinity(remaining) || remaining <= 0f)
            {
                throw new InvalidOperationException(
                    $"Patience warning event references customer visit {visit.EntityId} " +
                    $"with invalid remaining patience {remaining}.");
            }

            _notifications.Show(LocalizedTexts.Text(
                LocalizationKey.NotificationCustomerPatienceLow));
        }

        private void PresentAbandonment(GameEntity abandonedEvent)
        {
            ValidateEventKind(abandonedEvent, expectsWarning: false);
            GameEntity visit = ResolveVisit(abandonedEvent);
            if (visit.isOrder || !visit.isCustomerVisitAbandoning ||
                visit.isCustomerVisitWaitingForAbandonDeparture ||
                visit.isCustomerVisitAbandonDeparting ||
                visit.hasCustomerPatienceRemaining ||
                visit.isCustomerPatienceWarningIssued)
            {
                throw new InvalidOperationException(
                    $"Customer abandoned event references invalid customer visit " +
                    $"{visit.EntityId}.");
            }

            _notifications.Show(LocalizedTexts.Text(
                LocalizationKey.NotificationCustomerLeftImpatient));
        }

        private static void ValidateEventKind(
            GameEntity customerEvent,
            bool expectsWarning)
        {
            if (customerEvent.isCustomerPatienceWarningEvent != expectsWarning ||
                customerEvent.isCustomerAbandonedEvent == expectsWarning)
            {
                throw new InvalidOperationException(
                    "Customer patience event must contain exactly one event marker.");
            }
        }

        private GameEntity ResolveVisit(GameEntity customerEvent)
        {
            GameEntity visit = _gameContext.GetEntityWithEntityId(
                customerEvent.CustomerEventVisitEntityId);
            if (visit == null || visit.isDestructed || !visit.isCustomerVisit ||
                !visit.isCustomerVehicle || !visit.hasEntityId ||
                !visit.hasCustomerVisitStoreEntityId)
            {
                throw new InvalidOperationException(
                    $"Customer patience event references invalid visit " +
                    $"{customerEvent.CustomerEventVisitEntityId}.");
            }

            return visit;
        }
    }
}
