using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Cursor;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Consultation.Systems
{
    public sealed class ConfirmConsultationOfferSystem : IExecuteSystem
    {
        private const int OfferCount = 1;

        private readonly GameContext _gameContext;
        private readonly IOrderFactory _orderFactory;
        private readonly IGameEventFactory _events;
        private readonly ICursorService _cursor;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;
        private readonly List<GameEntity> _playerBuffer = new(1);
        private readonly List<GameEntity> _offers = new(OfferCount);
        private readonly List<GameEntity> _lineBuffer = new(8);

        public ConfirmConsultationOfferSystem(GameContext gameContext,
            InputContext inputContext, IOrderFactory orderFactory,
            IGameEventFactory events, ICursorService cursor)
        {
            _gameContext = gameContext;
            _orderFactory = orderFactory;
            _events = events;
            _cursor = cursor;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.StoreEntityId,
                GameMatcher.MoveDirection,
                GameMatcher.ModalOpen,
                GameMatcher.ConsultationVisitEntityId));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(
                InputMatcher.InputState,
                InputMatcher.ConfirmPressed));
        }

        public void Execute()
        {
            foreach (InputEntity ignored in _inputs)
            foreach (GameEntity player in _players.GetEntities(_playerBuffer))
                Confirm(player);
        }

        private void Confirm(GameEntity player)
        {
            GameEntity visit = _gameContext.GetEntityWithEntityId(
                player.ConsultationVisitEntityId);
            GameEntity customer = ValidateConsultingVisit(player, visit);

            CollectOffers(visit);
            GameEntity selectedOffer = null;
            foreach (GameEntity offer in _offers)
            {
                if (!offer.isSelectedConsultationOffer)
                    continue;
                if (selectedOffer != null)
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has more than one selected offer.");

                selectedOffer = offer;
            }

            if (selectedOffer == null)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has no selected offer.");

            int totalRequiredCount = CountRequiredProducts(selectedOffer);
            _orderFactory.AddOrderComponents(visit, selectedOffer);
            _events.EmitNotification(
                LocalizedTexts.Text(
                    LocalizationKey.NotificationOfferConfirmed,
                    LocalizedTexts.OfferTitle(
                        visit.CustomerProjectType,
                        selectedOffer.OfferIndex),
                    totalRequiredCount,
                    selectedOffer.OrderReward));
            visit.isCustomerVisitConsulting = false;
            visit.isCustomerVisitReturning = true;
            visit.RemoveServingOrderCounterEntityId();
            customer.RemoveReservedCustomerQueueSpotEntityId();
            DestructOffers();

            player.RemoveConsultationVisitEntityId();
            player.isModalOpen = false;
            player.ReplaceMoveDirection(Vector3.zero);
            if (player.hasInteractionPrompt)
                player.RemoveInteractionPrompt();
            player.isFocusInteractionAvailable = false;
            player.isCursorLocked = true;
            _cursor.SetLocked(true);
            _events.EmitAudio(AudioCueId.OrderAccepted);
        }

        private GameEntity ValidateConsultingVisit(
            GameEntity player,
            GameEntity visit)
        {
            if (visit == null || visit.isDestructed || !visit.isCustomerVisit ||
                !visit.isCustomerVehicle || !visit.isCustomerVisitConsulting ||
                visit.isOrder || !visit.hasEntityId ||
                !visit.hasCustomerVisitStoreEntityId ||
                visit.CustomerVisitStoreEntityId != player.StoreEntityId ||
                !visit.hasCustomerProjectType ||
                !visit.hasServingOrderCounterEntityId)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} cannot confirm an inactive consultation.");
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

            GameEntity orderCounter = _gameContext.GetEntityWithEntityId(
                visit.ServingOrderCounterEntityId);
            if (orderCounter == null || orderCounter.isDestructed ||
                !orderCounter.isOrderCounter || !orderCounter.hasEntityId ||
                !orderCounter.hasStoreEntityId ||
                orderCounter.StoreEntityId != visit.CustomerVisitStoreEntityId ||
                _gameContext.GetEntityWithServingOrderCounterEntityId(
                    orderCounter.EntityId) != visit)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid counter reservation.");
            }

            GameEntity customer =
                _gameContext.GetEntityWithCustomerActorVisitEntityId(visit.EntityId);
            if (customer == null || customer.isDestructed || !customer.isCustomer ||
                !customer.isRouteMover || !customer.isCustomerWaitingAtCounter ||
                !customer.hasEntityId || !customer.hasCustomerActorVisitEntityId ||
                customer.CustomerActorVisitEntityId != visit.EntityId ||
                !customer.hasReservedCustomerQueueSpotEntityId)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has no valid consulting actor.");
            }

            GameEntity queueSpot = _gameContext.GetEntityWithEntityId(
                customer.ReservedCustomerQueueSpotEntityId);
            if (queueSpot == null || queueSpot.isDestructed ||
                !queueSpot.isCustomerQueueSpot || !queueSpot.hasEntityId ||
                !queueSpot.hasCustomerQueueSpotStoreEntityId ||
                queueSpot.CustomerQueueSpotStoreEntityId !=
                visit.CustomerVisitStoreEntityId ||
                _gameContext.GetEntityWithReservedCustomerQueueSpotEntityId(
                    queueSpot.EntityId) != customer)
            {
                throw new InvalidOperationException(
                    $"Customer actor {customer.EntityId} has an invalid queue reservation.");
            }

            return customer;
        }

        private int CountRequiredProducts(GameEntity offer)
        {
            int totalRequiredCount = 0;
            foreach (GameEntity line in
                     _gameContext.GetEntitiesWithConsultationOfferEntityId(offer.EntityId))
            {
                if (!line.isConsultationOfferLine || line.isDestructed ||
                    !line.hasRequiredProductCount)
                {
                    throw new InvalidOperationException(
                        $"Consultation offer {offer.EntityId} has an invalid line.");
                }
                totalRequiredCount = checked(
                    totalRequiredCount + line.RequiredProductCount);
            }

            return totalRequiredCount;
        }

        private void DestructOffers()
        {
            _lineBuffer.Clear();
            foreach (GameEntity offer in _offers)
            {
                foreach (GameEntity line in
                         _gameContext.GetEntitiesWithConsultationOfferEntityId(offer.EntityId))
                {
                    if (!line.isDestructed)
                        _lineBuffer.Add(line);
                }
            }

            foreach (GameEntity line in _lineBuffer)
            {
                line.RemoveConsultationOfferEntityId();
                line.isDestructed = true;
            }
            foreach (GameEntity offer in _offers)
            {
                offer.RemoveConsultationOfferVisitEntityId();
                offer.isDestructed = true;
            }
        }

        private void CollectOffers(GameEntity visit)
        {
            _offers.Clear();
            foreach (GameEntity entity in
                     _gameContext.GetEntitiesWithConsultationOfferVisitEntityId(visit.EntityId))
            {
                if (entity.isConsultationOffer && !entity.isDestructed)
                    _offers.Add(entity);
            }

            if (_offers.Count != OfferCount)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} must have exactly {OfferCount} offers.");
        }
    }
}
