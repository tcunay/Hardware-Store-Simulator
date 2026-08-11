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
        private const int OfferCount = 3;

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
            if (!visit.isCustomerVisitConsulting || visit.isOrder)
                throw new InvalidOperationException(
                    $"Player cannot confirm inactive consultation {visit.EntityId}.");

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
            visit.isCustomerVisitLoading = true;
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
