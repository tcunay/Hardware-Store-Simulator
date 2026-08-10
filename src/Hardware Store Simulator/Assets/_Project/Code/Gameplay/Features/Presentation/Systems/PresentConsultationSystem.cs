using System;
using System.Linq;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Presentation;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Presentation.Systems
{
    public sealed class PresentConsultationSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IStaticDataService _staticData;
        private readonly IHudService _hud;
        private readonly IGroup<GameEntity> _players;

        public PresentConsultationSystem(GameContext gameContext,
            IStaticDataService staticData, IHudService hud)
        {
            _gameContext = gameContext;
            _staticData = staticData;
            _hud = hud;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.ConsultationVisitEntityId));
        }

        public void Execute()
        {
            _hud.PresentConsultation(null);

            GameEntity[] players = _players.GetEntities();
            if (players.Length > 1)
                throw new InvalidOperationException(
                    "The local HUD cannot present more than one consultation at a time.");
            if (players.Length == 0)
                return;

            GameEntity player = players[0];
            GameEntity visit =
                _gameContext.GetEntityWithEntityId(player.ConsultationVisitEntityId);
            ValidateVisit(visit);

            GameEntity[] offers = _gameContext
                .GetEntitiesWithCustomerVisitEntityId(visit.EntityId)
                .Where(offer => offer.isConsultationOffer && !offer.isDestructed)
                .ToArray();
            ValidateOffers(visit, offers);
            offers = offers.OrderBy(offer => offer.OfferIndex).ToArray();
            ValidateOfferOrder(visit, offers);

            var snapshots = new ConsultationOfferSnapshot[offers.Length];
            for (int index = 0; index < offers.Length; index++)
            {
                GameEntity offer = offers[index];
                ProductConfig product = _staticData.GetProduct(offer.RequiredProductType);
                snapshots[index] = new ConsultationOfferSnapshot(
                    offer.OfferIndex,
                    offer.OfferTitle,
                    offer.OfferDescription,
                    product.DisplayName,
                    product.UnitLabel,
                    offer.RequiredProductCount,
                    offer.AvailableProductCount,
                    offer.OrderReward,
                    offer.ExpectedProfit,
                    offer.isSelectedConsultationOffer);
            }

            _hud.PresentConsultation(new ConsultationSnapshot(
                visit.CustomerProjectTitle,
                visit.CustomerRequest,
                snapshots));
        }

        private static void ValidateVisit(GameEntity visit)
        {
            if (!visit.isCustomerVisit || !visit.isCustomerVisitConsulting ||
                !visit.hasEntityId || !visit.hasCustomerProjectTitle ||
                !visit.hasCustomerRequest)
            {
                throw new InvalidOperationException(
                    "An open consultation must reference a configured consulting customer visit.");
            }
        }

        private static void ValidateOffers(GameEntity visit, GameEntity[] offers)
        {
            if (offers.Length != 3)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} must expose exactly three consultation " +
                    $"offers, found {offers.Length}.");

            for (int index = 0; index < offers.Length; index++)
            {
                GameEntity offer = offers[index];
                if (!offer.hasOfferIndex || !offer.hasOfferTitle ||
                    !offer.hasOfferDescription ||
                    !offer.hasRequiredProductType || !offer.hasRequiredProductCount ||
                    !offer.hasAvailableProductCount || !offer.hasOrderReward ||
                    !offer.hasExpectedProfit)
                {
                    throw new InvalidOperationException(
                        $"Consultation offer at position {index} for customer visit " +
                        $"{visit.EntityId} is not fully configured.");
                }
            }

            int selectedCount = offers.Count(offer => offer.isSelectedConsultationOffer);
            if (selectedCount != 1)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} must have exactly one selected " +
                    $"consultation offer, found {selectedCount}.");
        }

        private static void ValidateOfferOrder(GameEntity visit, GameEntity[] offers)
        {
            for (int index = 0; index < offers.Length; index++)
            {
                if (offers[index].OfferIndex != index)
                {
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} must expose consultation offer " +
                        $"indices 0, 1 and 2 exactly once.");
                }
            }
        }
    }
}
