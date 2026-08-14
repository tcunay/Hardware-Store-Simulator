using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Factories;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class BeginCustomerAbandonmentSystem : IExecuteSystem
    {
        private const int ExpectedOfferCount = 3;

        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _visits;
        private readonly IGroup<GameEntity> _consultationPlayers;
        private readonly List<GameEntity> _visitBuffer = new(4);
        private readonly List<GameEntity> _playerBuffer = new(1);
        private readonly List<GameEntity> _offers = new(ExpectedOfferCount);
        private readonly List<GameEntity> _offerLines = new(6);

        public BeginCustomerAbandonmentSystem(GameContext gameContext,
            IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.EntityId,
                    GameMatcher.CustomerVisitStoreEntityId,
                    GameMatcher.CustomerArrivalSequence,
                    GameMatcher.CustomerPatienceRemaining,
                    GameMatcher.ReservedCustomerParkingSpotEntityId)
                .AnyOf(
                    GameMatcher.CustomerVisitQueued,
                    GameMatcher.CustomerVisitConsulting)
                .NoneOf(
                    GameMatcher.Order,
                    GameMatcher.Destructed));
            _consultationPlayers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Player,
                    GameMatcher.EntityId,
                    GameMatcher.ModalOpen,
                    GameMatcher.ConsultationVisitEntityId)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            _consultationPlayers.GetEntities(_playerBuffer);
            foreach (GameEntity visit in _visits.GetEntities(_visitBuffer))
            {
                float remaining = visit.CustomerPatienceRemaining;
                if (float.IsNaN(remaining) || float.IsInfinity(remaining) || remaining < 0f)
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has invalid patience {remaining}.");
                if (remaining > 0f || HasActiveConsultationModal(visit))
                    continue;

                BeginAbandonment(visit);
            }
        }

        private void BeginAbandonment(GameEntity visit)
        {
            if (visit.isCustomerVisitQueued == visit.isCustomerVisitConsulting ||
                visit.CustomerArrivalSequence < 0 ||
                visit.hasReservedCustomerTrafficLaneEntityId ||
                visit.hasReservedCustomerLoadingBayEntityId ||
                visit.hasRoute || visit.hasRouteWaypointIndex || visit.isRouteCompleted ||
                visit.isInteractable)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} cannot begin abandonment.");
            }

            GameEntity store = _gameContext.GetEntityWithEntityId(
                visit.CustomerVisitStoreEntityId);
            ValidateStore(visit, store);
            GameEntity parkingSpot = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerParkingSpotEntityId);
            ValidateParkingSpot(visit, parkingSpot);
            GameEntity actor = _gameContext.GetEntityWithCustomerActorVisitEntityId(
                visit.EntityId);
            ValidateActor(visit, actor);
            GameEntity queueSpot = ValidateQueueReservation(visit, actor);
            ValidateCounterReservation(visit, store);
            CollectAndValidateOffers(visit);

            Pose[] returnRoute = CreateAbandonReturnRoute(actor, queueSpot);
            int nextLostCount = checked(store.DayLostCustomerCount + 1);

            ClearActorRoute(actor);
            actor.RemoveReservedCustomerQueueSpotEntityId();
            actor.RemoveCustomerReturnRoute();
            actor.isCustomerApproachingCounter = false;
            actor.isCustomerWaitingInQueue = false;
            actor.isCustomerWaitingAtCounter = false;
            actor.isCustomerAbandonReturningToVehicle = true;
            actor.AddRoute(returnRoute);
            actor.AddRouteWaypointIndex(1);

            visit.isCustomerVisitQueued = false;
            visit.isCustomerVisitConsulting = false;
            visit.isCustomerVisitAbandoning = true;
            if (visit.hasServingOrderCounterEntityId)
                visit.RemoveServingOrderCounterEntityId();
            visit.RemoveCustomerPatienceRemaining();
            if (visit.isCustomerPatienceWarningIssued)
                visit.isCustomerPatienceWarningIssued = false;

            DestructOffers();
            store.ReplaceDayLostCustomerCount(nextLostCount);
            _events.EmitCustomerAbandoned(visit.EntityId);
        }

        private bool HasActiveConsultationModal(GameEntity visit)
        {
            GameEntity owner = null;
            foreach (GameEntity player in _playerBuffer)
            {
                if (player.ConsultationVisitEntityId != visit.EntityId)
                    continue;
                if (!visit.isCustomerVisitConsulting)
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} has a modal for non-consulting customer " +
                        $"visit {visit.EntityId}.");
                if (owner != null)
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has more than one consultation modal.");
                owner = player;
            }

            return owner != null;
        }

        private static void ValidateStore(GameEntity visit, GameEntity store)
        {
            if (store == null || store.isDestructed || !store.isStore ||
                !store.hasEntityId || !store.hasDayLostCustomerCount ||
                store.DayLostCustomerCount < 0 ||
                (!store.isStoreOpen && !store.isStoreClosing) ||
                store.isStoreOpen == store.isStoreClosing)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} references an invalid active store.");
            }
        }

        private static void ValidateParkingSpot(
            GameEntity visit,
            GameEntity parkingSpot)
        {
            if (parkingSpot == null || parkingSpot.isDestructed ||
                !parkingSpot.isCustomerParkingSpot || !parkingSpot.hasEntityId ||
                !parkingSpot.hasCustomerParkingSpotStoreEntityId ||
                !parkingSpot.hasCustomerVehicleParkingDepartureRoute ||
                parkingSpot.CustomerParkingSpotStoreEntityId !=
                visit.CustomerVisitStoreEntityId)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid parking reservation.");
            }
        }

        private static void ValidateActor(GameEntity visit, GameEntity actor)
        {
            if (actor == null || actor.isDestructed || !actor.isCustomer ||
                !actor.isRouteMover || !actor.hasEntityId ||
                !actor.hasCustomerActorVisitEntityId ||
                actor.CustomerActorVisitEntityId != visit.EntityId ||
                !actor.hasReservedCustomerQueueSpotEntityId ||
                !actor.hasCustomerReturnRoute || !actor.hasTransform ||
                !actor.hasRigidbody || !actor.hasMovementSpeed ||
                !actor.hasRotationSpeed || !actor.hasWaypointTolerance ||
                actor.isCustomerReturningToVehicle ||
                actor.isCustomerAbandonReturningToVehicle)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has no valid abandoning actor.");
            }

            int actorStateCount =
                (actor.isCustomerApproachingCounter ? 1 : 0) +
                (actor.isCustomerWaitingInQueue ? 1 : 0) +
                (actor.isCustomerWaitingAtCounter ? 1 : 0);
            if (actorStateCount != 1 ||
                visit.isCustomerVisitConsulting != actor.isCustomerWaitingAtCounter)
            {
                throw new InvalidOperationException(
                    $"Customer actor {actor.EntityId} has invalid pre-abandon lifecycle.");
            }
            if (!actor.Rigidbody.isKinematic ||
                actor.Rigidbody.interpolation != RigidbodyInterpolation.None)
            {
                throw new InvalidOperationException(
                    $"Customer actor {actor.EntityId} has invalid route rigidbody settings.");
            }
        }

        private GameEntity ValidateQueueReservation(GameEntity visit, GameEntity actor)
        {
            GameEntity queueSpot = _gameContext.GetEntityWithEntityId(
                actor.ReservedCustomerQueueSpotEntityId);
            if (queueSpot == null || queueSpot.isDestructed ||
                !queueSpot.isCustomerQueueSpot || !queueSpot.hasEntityId ||
                !queueSpot.hasCustomerQueueSpotStoreEntityId ||
                !queueSpot.hasQueueSpotIndex ||
                !queueSpot.hasCustomerQueueAbandonRoute ||
                queueSpot.CustomerQueueSpotStoreEntityId !=
                visit.CustomerVisitStoreEntityId ||
                _gameContext.GetEntityWithReservedCustomerQueueSpotEntityId(
                    queueSpot.EntityId) != actor)
            {
                throw new InvalidOperationException(
                    $"Customer actor {actor.EntityId} has an invalid queue reservation.");
            }

            return queueSpot;
        }

        private void ValidateCounterReservation(GameEntity visit, GameEntity store)
        {
            if (!visit.isCustomerVisitConsulting)
            {
                if (visit.hasServingOrderCounterEntityId)
                    throw new InvalidOperationException(
                        $"Queued customer visit {visit.EntityId} unexpectedly owns a counter.");
                return;
            }
            if (!visit.hasServingOrderCounterEntityId ||
                !store.hasOrderCounterEntityId ||
                visit.ServingOrderCounterEntityId != store.OrderCounterEntityId)
            {
                throw new InvalidOperationException(
                    $"Consulting customer visit {visit.EntityId} has invalid counter ownership.");
            }

            GameEntity counter = _gameContext.GetEntityWithEntityId(
                visit.ServingOrderCounterEntityId);
            if (counter == null || counter.isDestructed || !counter.isOrderCounter ||
                !counter.hasStoreEntityId ||
                counter.StoreEntityId != visit.CustomerVisitStoreEntityId ||
                _gameContext.GetEntityWithServingOrderCounterEntityId(
                    counter.EntityId) != visit)
            {
                throw new InvalidOperationException(
                    $"Consulting customer visit {visit.EntityId} references an invalid counter.");
            }
        }

        private void CollectAndValidateOffers(GameEntity visit)
        {
            _offers.Clear();
            _offerLines.Clear();
            foreach (GameEntity offer in
                     _gameContext.GetEntitiesWithConsultationOfferVisitEntityId(
                         visit.EntityId))
            {
                if (offer.isDestructed || !offer.isConsultationOffer ||
                    !offer.hasEntityId || !offer.hasOfferIndex ||
                    !offer.hasOrderReward || !offer.hasExpectedProfit ||
                    offer.ConsultationOfferVisitEntityId != visit.EntityId)
                {
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has an invalid consultation offer.");
                }

                _offers.Add(offer);
                foreach (GameEntity line in
                         _gameContext.GetEntitiesWithConsultationOfferEntityId(
                             offer.EntityId))
                {
                    if (line.isDestructed || !line.isConsultationOfferLine ||
                        !line.hasEntityId || !line.hasConsultationOfferEntityId ||
                        line.ConsultationOfferEntityId != offer.EntityId)
                    {
                        throw new InvalidOperationException(
                            $"Consultation offer {offer.EntityId} has an invalid line.");
                    }
                    _offerLines.Add(line);
                }
            }
            if (_offers.Count != ExpectedOfferCount)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} must own exactly " +
                    $"{ExpectedOfferCount} consultation offers before abandonment.");
        }

        private static Pose[] CreateAbandonReturnRoute(
            GameEntity actor,
            GameEntity queueSpot)
        {
            Pose[] customerReturn = actor.CustomerReturnRoute;
            if (customerReturn == null || customerReturn.Length < 2)
                throw new InvalidOperationException(
                    $"Customer actor {actor.EntityId} has an invalid authored return route.");
            Pose[] queueExit = queueSpot.CustomerQueueAbandonRoute;
            if (queueExit == null || queueExit.Length < 2)
                throw new InvalidOperationException(
                    $"Customer queue spot {queueSpot.EntityId} has an invalid abandon route.");
            for (int index = 0; index < queueExit.Length; index++)
                ValidatePose(queueExit[index], actor.EntityId, index + 1);
            for (int index = 1; index < customerReturn.Length; index++)
                ValidatePose(
                    customerReturn[index],
                    actor.EntityId,
                    queueExit.Length + index);
            RequireMatchingJoin(
                queueExit[^1],
                customerReturn[1],
                actor.EntityId);

            Pose current = new(actor.Rigidbody.position, actor.Rigidbody.rotation);
            ValidatePose(current, actor.EntityId, 0);

            var route = new Pose[
                1 + queueExit.Length + customerReturn.Length - 2];
            route[0] = current;
            Array.Copy(queueExit, 0, route, 1, queueExit.Length);
            Array.Copy(
                customerReturn,
                2,
                route,
                1 + queueExit.Length,
                customerReturn.Length - 2);
            return route;
        }

        private static void RequireMatchingJoin(
            Pose queueExit,
            Pose customerReturn,
            int actorEntityId)
        {
            if (Vector3.Distance(queueExit.position, customerReturn.position) > 0.05f ||
                Quaternion.Angle(queueExit.rotation, customerReturn.rotation) > 0.1f)
            {
                throw new InvalidOperationException(
                    $"Customer actor {actorEntityId} abandon and return routes do not join.");
            }
        }

        private static void ValidatePose(Pose pose, int actorEntityId, int index)
        {
            Vector3 position = pose.position;
            Quaternion rotation = pose.rotation;
            if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z) ||
                !IsFinite(rotation.x) || !IsFinite(rotation.y) ||
                !IsFinite(rotation.z) || !IsFinite(rotation.w) ||
                Quaternion.Dot(rotation, rotation) <= 0f)
            {
                throw new InvalidOperationException(
                    $"Customer actor {actorEntityId} has invalid return pose {index}.");
            }
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ClearActorRoute(GameEntity actor)
        {
            if (actor.hasRoute)
                actor.RemoveRoute();
            if (actor.hasRouteWaypointIndex)
                actor.RemoveRouteWaypointIndex();
            if (actor.isRouteCompleted)
                actor.isRouteCompleted = false;
        }

        private void DestructOffers()
        {
            foreach (GameEntity line in _offerLines)
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
    }
}
