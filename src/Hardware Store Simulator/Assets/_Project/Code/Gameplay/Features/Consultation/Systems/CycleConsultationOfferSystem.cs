using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Consultation.Systems
{
    public sealed class CycleConsultationOfferSystem : IExecuteSystem
    {
        private const int OfferCount = 3;

        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;
        private readonly List<GameEntity> _offers = new(OfferCount);

        public CycleConsultationOfferSystem(GameContext gameContext,
            InputContext inputContext)
        {
            _gameContext = gameContext;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.ModalOpen,
                GameMatcher.ConsultationVisitEntityId));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(InputMatcher.InputState)
                .AnyOf(InputMatcher.PreviousPressed, InputMatcher.NextPressed));
        }

        public void Execute()
        {
            foreach (InputEntity input in _inputs)
            {
                int offset = (input.isNextPressed ? 1 : 0) -
                             (input.isPreviousPressed ? 1 : 0);
                if (offset == 0)
                    continue;

                foreach (GameEntity player in _players)
                    Cycle(player, offset);
            }
        }

        private void Cycle(GameEntity player, int offset)
        {
            GameEntity visit = _gameContext.GetEntityWithEntityId(
                player.ConsultationVisitEntityId);
            if (!visit.isCustomerVisitConsulting)
                throw new InvalidOperationException(
                    $"Player consultation references inactive visit {visit.EntityId}.");

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

            int nextIndex =
                (selectedOffer.OfferIndex + offset + OfferCount) % OfferCount;
            GameEntity nextOffer = null;
            foreach (GameEntity offer in _offers)
            {
                if (offer.OfferIndex != nextIndex)
                    continue;
                if (nextOffer != null)
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has duplicate offer index {nextIndex}.");

                nextOffer = offer;
            }

            if (nextOffer == null)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has no offer index {nextIndex}.");

            selectedOffer.isSelectedConsultationOffer = false;
            nextOffer.isSelectedConsultationOffer = true;
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
