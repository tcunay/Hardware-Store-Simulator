using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Scene;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class CompleteCustomerVehicleArrivalSystem : IExecuteSystem
    {
        private const int ConsultationOfferCount = 3;

        private readonly GameContext _gameContext;
        private readonly ICustomerFactory _customerFactory;
        private readonly IStoreSceneData _sceneData;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public CompleteCustomerVehicleArrivalSystem(GameContext gameContext,
            ICustomerFactory customerFactory, IStoreSceneData sceneData)
        {
            _gameContext = gameContext;
            _customerFactory = customerFactory;
            _sceneData = sceneData;
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
                if (_gameContext.GetEntityWithCustomerActorVisitEntityId(visit.EntityId) != null)
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} already has a customer actor.");

                Pose[] routeToCounter =
                    _sceneData.GetRoute(SceneRouteId.CustomerWalkToCounter);
                Pose[] routeToVehicle =
                    _sceneData.GetRoute(SceneRouteId.CustomerWalkToVehicle);
                _customerFactory.Create(visit, routeToCounter, routeToVehicle);

                visit.isRouteCompleted = false;
                visit.RemoveRoute();
                visit.RemoveRouteWaypointIndex();
            }
        }
    }
}
