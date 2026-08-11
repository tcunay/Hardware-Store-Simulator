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
        private const int OfferCount = 3;
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

            GameEntity visit = _gameContext.GetEntityWithEntityId(
                players[0].ConsultationVisitEntityId);
            ValidateVisit(visit);

            GameEntity[] offers = _gameContext
                .GetEntitiesWithConsultationOfferVisitEntityId(visit.EntityId)
                .Where(offer => offer.isConsultationOffer && !offer.isDestructed)
                .OrderBy(offer => offer.OfferIndex)
                .ToArray();
            ValidateOffers(visit, offers);

            var snapshots = new ConsultationOfferSnapshot[offers.Length];
            for (int index = 0; index < offers.Length; index++)
                snapshots[index] = CreateOfferSnapshot(visit, offers[index]);

            _hud.PresentConsultation(new ConsultationSnapshot(
                visit.CustomerProjectType,
                visit.Slots.Length,
                snapshots));
        }

        private ConsultationOfferSnapshot CreateOfferSnapshot(
            GameEntity visit,
            GameEntity offer)
        {
            GameEntity[] lines = _gameContext
                .GetEntitiesWithConsultationOfferEntityId(offer.EntityId)
                .Where(line => line.isConsultationOfferLine && !line.isDestructed)
                .OrderBy(line => line.LineIndex)
                .ToArray();
            ValidateLines(visit, offer, lines);

            var lineSnapshots = new ConsultationOfferLineSnapshot[lines.Length];
            int totalUnitCount = 0;
            int productCost = 0;
            for (int index = 0; index < lines.Length; index++)
            {
                GameEntity line = lines[index];
                DeliveryConfig delivery = _staticData.GetDelivery(line.ProductType);
                totalUnitCount = checked(totalUnitCount + line.RequiredProductCount);
                productCost = checked(
                    productCost + delivery.PurchaseUnitPrice * line.RequiredProductCount);
                lineSnapshots[index] = new ConsultationOfferLineSnapshot(
                    line.LineIndex,
                    line.ProductType,
                    line.AvailableProductCount,
                    line.RequiredProductCount);
            }

            if (totalUnitCount > visit.Slots.Length)
                throw new InvalidOperationException(
                    $"Consultation offer {offer.EntityId} requires {totalUnitCount} cargo " +
                    $"slots, but customer visit {visit.EntityId} has {visit.Slots.Length}.");
            int expectedProfit = checked(offer.OrderReward - productCost);
            if (offer.ExpectedProfit != expectedProfit)
                throw new InvalidOperationException(
                    $"Consultation offer {offer.EntityId} has expected profit " +
                    $"{offer.ExpectedProfit}, calculated value is {expectedProfit}.");

            return new ConsultationOfferSnapshot(
                offer.OfferIndex,
                lineSnapshots,
                totalUnitCount,
                productCost,
                offer.OrderReward,
                offer.ExpectedProfit,
                offer.isSelectedConsultationOffer);
        }

        private static void ValidateVisit(GameEntity visit)
        {
            if (!visit.isCustomerVisit || !visit.isCustomerVisitConsulting ||
                !visit.hasEntityId || !visit.hasCustomerProjectType ||
                !visit.hasSlots)
            {
                throw new InvalidOperationException(
                    "An open consultation must reference a configured consulting customer visit.");
            }
            if (visit.Slots.Length == 0)
                throw new InvalidOperationException(
                    $"Consulting customer visit {visit.EntityId} has no cargo slots.");
        }

        private static void ValidateOffers(GameEntity visit, GameEntity[] offers)
        {
            if (offers.Length != OfferCount)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} must expose exactly {OfferCount} " +
                    $"consultation offers, found {offers.Length}.");

            int selectedCount = 0;
            for (int index = 0; index < offers.Length; index++)
            {
                GameEntity offer = offers[index];
                if (!offer.hasEntityId || !offer.hasConsultationOfferVisitEntityId ||
                    !offer.hasOfferIndex || !offer.hasOrderReward ||
                    !offer.hasExpectedProfit)
                {
                    throw new InvalidOperationException(
                        $"Consultation offer at position {index} for customer visit " +
                        $"{visit.EntityId} is not fully configured.");
                }
                if (offer.ConsultationOfferVisitEntityId != visit.EntityId ||
                    offer.OfferIndex != index)
                {
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} must expose offer indices 0, 1 and " +
                        "2 exactly once.");
                }
                if (offer.isSelectedConsultationOffer)
                    selectedCount++;
            }

            if (selectedCount != 1)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} must have exactly one selected " +
                    $"consultation offer, found {selectedCount}.");
        }

        private static void ValidateLines(
            GameEntity visit,
            GameEntity offer,
            GameEntity[] lines)
        {
            if (lines.Length == 0 ||
                lines.Length > CustomerProjectConfig.MaxLinesPerOffer)
                throw new InvalidOperationException(
                    $"Consultation offer {offer.EntityId} must expose between one and " +
                    $"{CustomerProjectConfig.MaxLinesPerOffer} product lines, found " +
                    $"{lines.Length}.");

            for (int index = 0; index < lines.Length; index++)
            {
                GameEntity line = lines[index];
                if (!line.hasConsultationOfferEntityId || !line.hasLineIndex ||
                    !line.hasProductType || !line.hasRequiredProductCount ||
                    !line.hasAvailableProductCount)
                {
                    throw new InvalidOperationException(
                        $"Consultation offer line at position {index} for offer " +
                        $"{offer.EntityId} is not fully configured.");
                }
                if (line.ConsultationOfferEntityId != offer.EntityId ||
                    line.LineIndex != index || line.RequiredProductCount <= 0 ||
                    line.AvailableProductCount < 0)
                {
                    throw new InvalidOperationException(
                        $"Consultation offer {offer.EntityId} has an invalid line at " +
                        $"position {index}.");
                }
                for (int previous = 0; previous < index; previous++)
                {
                    if (lines[previous].ProductType == line.ProductType)
                        throw new InvalidOperationException(
                            $"Consultation offer {offer.EntityId} contains duplicate product " +
                            $"type {line.ProductType} for customer visit {visit.EntityId}.");
                }
            }
        }
    }
}
