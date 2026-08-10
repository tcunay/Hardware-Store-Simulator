using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class CompleteCustomerVehicleArrivalSystem : IExecuteSystem
    {
        private const int ConsultationOfferCount = 3;

        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public CompleteCustomerVehicleArrivalSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.CustomerVisitArriving,
                    GameMatcher.EntityId,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.RequestedProductType,
                    GameMatcher.CustomerProjectTitle,
                    GameMatcher.CustomerRequest,
                    GameMatcher.LoadingZone,
                    GameMatcher.Slots,
                    GameMatcher.Route,
                    GameMatcher.RouteWaypointIndex,
                    GameMatcher.RouteCompleted)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
            {
                int offerCount = 0;
                int selectedOfferCount = 0;
                int maximumRequiredProductCount = 0;
                foreach (GameEntity entity in
                         _gameContext.GetEntitiesWithCustomerVisitEntityId(visit.EntityId))
                {
                    if (!entity.isConsultationOffer || entity.isDestructed)
                        continue;
                    if (!entity.hasOfferIndex || !entity.hasOfferTitle ||
                        !entity.hasOfferDescription || !entity.hasRequiredProductType ||
                        !entity.hasRequiredProductCount ||
                        !entity.hasAvailableProductCount || !entity.hasOrderReward ||
                        !entity.hasExpectedProfit || !entity.hasStorageZoneEntityId)
                    {
                        throw new InvalidOperationException(
                            $"Customer visit {visit.EntityId} has an incomplete offer entity.");
                    }
                    if (entity.RequiredProductType != visit.RequestedProductType ||
                        entity.StorageZoneEntityId != visit.StorageZoneEntityId)
                    {
                        throw new InvalidOperationException(
                            $"Customer visit {visit.EntityId} has an offer for another " +
                            "product or storage zone.");
                    }

                    offerCount++;
                    if (entity.isSelectedConsultationOffer)
                        selectedOfferCount++;
                    maximumRequiredProductCount = Math.Max(
                        maximumRequiredProductCount,
                        entity.RequiredProductCount);
                }

                if (offerCount != ConsultationOfferCount || selectedOfferCount != 1)
                {
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} requires exactly " +
                        $"{ConsultationOfferCount} offers and one selection.");
                }
                if (visit.Slots.Length < maximumRequiredProductCount)
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has {visit.Slots.Length} loading slots, " +
                        $"but its largest offer requires {maximumRequiredProductCount} products.");

                visit.isCustomerVisitArriving = false;
                visit.isRouteCompleted = false;
                visit.RemoveRoute();
                visit.RemoveRouteWaypointIndex();
                visit.isCustomerVisitConsulting = true;
                visit.isInteractable = true;
            }
        }
    }
}
