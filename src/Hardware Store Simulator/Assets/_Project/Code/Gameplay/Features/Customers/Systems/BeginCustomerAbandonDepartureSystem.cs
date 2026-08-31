using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class BeginCustomerAbandonDepartureSystem : IExecuteSystem
    {
        private const float StartPositionTolerance = 0.05f;
        private const float StartRotationTolerance = 0.1f;

        private readonly GameContext _gameContext;
        private readonly CustomerVehicleConfig _config;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public BeginCustomerAbandonDepartureSystem(GameContext gameContext,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _config = staticData.CustomerVehicle;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.VehicleTrafficControlled,
                    GameMatcher.CustomerVisitWaitingForAbandonDeparture,
                    GameMatcher.EntityId,
                    GameMatcher.CustomerVisitStoreEntityId,
                    GameMatcher.ReservedCustomerParkingSpotEntityId,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody,
                    GameMatcher.MovementSpeed,
                    GameMatcher.RotationSpeed,
                    GameMatcher.WaypointTolerance)
                .NoneOf(
                    GameMatcher.Order,
                    GameMatcher.ReservedCustomerTrafficLaneEntityId,
                    GameMatcher.ReservedCustomerLoadingBayEntityId,
                    GameMatcher.ServingOrderCounterEntityId,
                    GameMatcher.CustomerPatienceRemaining,
                    GameMatcher.Route,
                    GameMatcher.RouteWaypointIndex,
                    GameMatcher.RouteCompleted,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
                TryBeginDeparture(visit);
        }

        private void TryBeginDeparture(GameEntity visit)
        {
            if (_gameContext.GetEntityWithCustomerActorVisitEntityId(visit.EntityId) != null ||
                _gameContext.GetEntitiesWithConsultationOfferVisitEntityId(
                    visit.EntityId).Count != 0 || visit.isInteractable)
            {
                throw new InvalidOperationException(
                    $"Abandoned customer visit {visit.EntityId} is not ready to depart.");
            }

            GameEntity store = _gameContext.GetEntityWithEntityId(
                visit.CustomerVisitStoreEntityId);
            if (store == null || store.isDestructed || !store.isStore ||
                !store.hasEntityId || (!store.isStoreOpen && !store.isStoreClosing) ||
                store.isStoreOpen == store.isStoreClosing)
            {
                throw new InvalidOperationException(
                    $"Abandoned customer visit {visit.EntityId} references an invalid store.");
            }

            GameEntity trafficLane =
                _gameContext.GetEntityWithCustomerTrafficLaneStoreEntityId(
                    visit.CustomerVisitStoreEntityId);
            if (trafficLane == null || trafficLane.isDestructed ||
                !trafficLane.isCustomerTrafficLane || !trafficLane.hasEntityId ||
                !trafficLane.hasCustomerTrafficLaneStoreEntityId ||
                trafficLane.CustomerTrafficLaneStoreEntityId !=
                visit.CustomerVisitStoreEntityId)
            {
                throw new InvalidOperationException(
                    $"Abandoned customer visit {visit.EntityId} has an invalid traffic lane.");
            }
            if (_gameContext.GetEntityWithReservedCustomerTrafficLaneEntityId(
                    trafficLane.EntityId) != null)
            {
                return;
            }

            GameEntity parkingSpot = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerParkingSpotEntityId);
            if (parkingSpot == null || parkingSpot.isDestructed ||
                !parkingSpot.isCustomerParkingSpot || !parkingSpot.hasEntityId ||
                !parkingSpot.hasCustomerParkingSpotStoreEntityId ||
                !parkingSpot.hasCustomerVehicleParkingDepartureRoute ||
                parkingSpot.CustomerParkingSpotStoreEntityId !=
                visit.CustomerVisitStoreEntityId ||
                _gameContext.GetEntityWithReservedCustomerParkingSpotEntityId(
                    parkingSpot.EntityId) != visit)
            {
                throw new InvalidOperationException(
                    $"Abandoned customer visit {visit.EntityId} has an invalid parking spot.");
            }

            Pose[] authoredRoute = parkingSpot.CustomerVehicleParkingDepartureRoute;
            if (!visit.isVehicleTrafficControlled)
                ValidateRouteStart(visit, authoredRoute);
            Pose[] route = (Pose[])authoredRoute.Clone();

            visit.AddReservedCustomerTrafficLaneEntityId(trafficLane.EntityId);
            visit.AddRoute(route);
            visit.AddRouteWaypointIndex(1);
            visit.ReplaceMovementSpeed(_config.DepartureSpeed);
            visit.isCustomerVisitWaitingForAbandonDeparture = false;
            visit.isCustomerVisitAbandonDeparting = true;
        }

        private static void ValidateRouteStart(GameEntity visit, Pose[] route)
        {
            if (route == null || route.Length < 2)
                throw new InvalidOperationException(
                    $"Abandoned customer visit {visit.EntityId} has an invalid parking " +
                    "departure route.");
            Pose start = route[0];
            if ((visit.Rigidbody.position - start.position).sqrMagnitude >
                StartPositionTolerance * StartPositionTolerance ||
                Quaternion.Angle(visit.Rigidbody.rotation, start.rotation) >
                StartRotationTolerance)
            {
                throw new InvalidOperationException(
                    $"Abandoned customer visit {visit.EntityId} is not at the authored " +
                    "parking departure start.");
            }
        }
    }
}
