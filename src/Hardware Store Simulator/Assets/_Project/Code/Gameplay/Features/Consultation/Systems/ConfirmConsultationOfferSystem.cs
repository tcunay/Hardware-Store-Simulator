using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Cursor;
using HardwareStore.Gameplay.Factories;
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

            _orderFactory.AddOrderComponents(visit, selectedOffer);
            _events.EmitNotification(
                $"Предложение сформировано • {selectedOffer.OfferTitle} • объём: " +
                $"{selectedOffer.RequiredProductCount} • сумма: " +
                $"{selectedOffer.OrderReward:N0} ₽");
            visit.RemoveRequestedProductType();
            visit.isCustomerVisitConsulting = false;
            visit.isCustomerVisitWaiting = true;
            foreach (GameEntity offer in _offers)
                offer.isDestructed = true;

            player.RemoveConsultationVisitEntityId();
            player.ReplaceMoveDirection(Vector3.zero);
            if (player.hasInteractionPrompt)
                player.RemoveInteractionPrompt();
            player.isFocusInteractionAvailable = false;
            player.isCursorLocked = true;
            _cursor.SetLocked(true);
        }

        private void CollectOffers(GameEntity visit)
        {
            _offers.Clear();
            foreach (GameEntity entity in
                     _gameContext.GetEntitiesWithCustomerVisitEntityId(visit.EntityId))
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
