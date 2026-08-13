using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class ValidateCustomerFlowStateSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly CustomerFlowConfig _config;
        private readonly IGroup<GameEntity> _stores;
        private readonly List<GameEntity> _storeBuffer = new(1);
        private readonly List<GameEntity> _queueVisits = new(4);
        private readonly HashSet<int> _arrivalSequences = new();

        public ValidateCustomerFlowStateSystem(GameContext gameContext,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _config = staticData.CustomerFlow;
            _stores = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Store,
                    GameMatcher.EntityId,
                    GameMatcher.NextCustomerArrivalSequence)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity store in _stores.GetEntities(_storeBuffer))
                ValidateStore(store);
        }

        private void ValidateStore(GameEntity store)
        {
            if (store.NextCustomerArrivalSequence < 0)
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has invalid next customer sequence.");
            int parkingCount =
                _gameContext.GetEntitiesWithCustomerParkingSpotStoreEntityId(
                    store.EntityId).Count;
            int queueSpotCount =
                _gameContext.GetEntitiesWithCustomerQueueSpotStoreEntityId(
                    store.EntityId).Count;
            if (parkingCount != _config.ParkingCapacity ||
                queueSpotCount != _config.ParkingCapacity)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} customer flow requires " +
                    $"{_config.ParkingCapacity} parking and queue spots, found " +
                    $"{parkingCount} and {queueSpotCount}.");
            }
            ValidateBay(store);
            ValidateTrafficLane(store);

            if (store.isStoreOpen)
            {
                if (!store.hasCustomerCooldownRemaining)
                    throw new InvalidOperationException(
                        $"Open store {store.EntityId} must retain its arrival cooldown.");
                float cooldown = store.CustomerCooldownRemaining;
                if (float.IsNaN(cooldown) || float.IsInfinity(cooldown) || cooldown < 0f)
                    throw new InvalidOperationException(
                        $"Open store {store.EntityId} has invalid arrival cooldown.");
            }
            else if (store.hasCustomerCooldownRemaining)
            {
                throw new InvalidOperationException(
                    $"Non-open store {store.EntityId} cannot schedule customer arrivals.");
            }

            _queueVisits.Clear();
            _arrivalSequences.Clear();
            foreach (GameEntity visit in
                     _gameContext.GetEntitiesWithCustomerVisitStoreEntityId(store.EntityId))
            {
                ValidateVisit(store, visit);
                if (!_arrivalSequences.Add(visit.CustomerArrivalSequence))
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} has duplicate customer arrival sequence " +
                        $"{visit.CustomerArrivalSequence}.");
                if (visit.isCustomerVisitQueued || visit.isCustomerVisitConsulting)
                    _queueVisits.Add(visit);
            }

            ValidateFifoQueue(store);
        }

        private void ValidateVisit(GameEntity store, GameEntity visit)
        {
            if (visit == null || visit.isDestructed || !visit.isCustomerVisit ||
                !visit.isCustomerVehicle || !visit.isRouteMover ||
                !visit.isLoadingZone || !visit.hasEntityId ||
                !visit.hasCustomerVisitStoreEntityId ||
                !visit.hasCustomerArrivalSequence ||
                visit.CustomerVisitStoreEntityId != store.EntityId ||
                visit.CustomerArrivalSequence < 0 ||
                visit.CustomerArrivalSequence >= store.NextCustomerArrivalSequence)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid customer visit.");
            }

            int lifecycleCount =
                (visit.isCustomerVisitArriving ? 1 : 0) +
                (visit.isCustomerVisitQueued ? 1 : 0) +
                (visit.isCustomerVisitConsulting ? 1 : 0) +
                (visit.isCustomerVisitReturning ? 1 : 0) +
                (visit.isCustomerVisitWaitingForLoadingBay ? 1 : 0) +
                (visit.isCustomerVisitMovingToLoadingBay ? 1 : 0) +
                (visit.isCustomerVisitLoading ? 1 : 0) +
                (visit.isCustomerVisitCompleted ? 1 : 0) +
                (visit.isCustomerVisitDeparting ? 1 : 0);
            if (lifecycleCount != 1)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} must have exactly one lifecycle state.");

            bool expectsParking = visit.isCustomerVisitArriving ||
                                  visit.isCustomerVisitQueued ||
                                  visit.isCustomerVisitConsulting ||
                                  visit.isCustomerVisitReturning ||
                                  visit.isCustomerVisitWaitingForLoadingBay ||
                                  visit.isCustomerVisitMovingToLoadingBay;
            if (visit.hasReservedCustomerParkingSpotEntityId != expectsParking)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has invalid parking ownership.");
            if (expectsParking)
                ValidateParkingRelation(store, visit);

            bool expectsLane = visit.isCustomerVisitArriving ||
                               visit.isCustomerVisitMovingToLoadingBay ||
                               visit.isCustomerVisitDeparting;
            if (visit.hasReservedCustomerTrafficLaneEntityId != expectsLane)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has invalid traffic-lane ownership.");
            if (expectsLane)
                ValidateLaneRelation(store, visit);

            bool requiresBay = visit.isCustomerVisitMovingToLoadingBay ||
                               visit.isCustomerVisitLoading ||
                               visit.isCustomerVisitCompleted ||
                               visit.isCustomerVisitDeparting;
            bool allowsBay = requiresBay || visit.isCustomerVisitReturning ||
                             visit.isCustomerVisitWaitingForLoadingBay;
            if ((requiresBay && !visit.hasReservedCustomerLoadingBayEntityId) ||
                (!allowsBay && visit.hasReservedCustomerLoadingBayEntityId))
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has invalid loading-bay ownership.");
            }
            if (visit.hasReservedCustomerLoadingBayEntityId)
                ValidateBayRelation(store, visit);

            bool expectsOrder = visit.isCustomerVisitReturning ||
                                visit.isCustomerVisitWaitingForLoadingBay ||
                                visit.isCustomerVisitMovingToLoadingBay ||
                                visit.isCustomerVisitLoading ||
                                visit.isCustomerVisitCompleted ||
                                visit.isCustomerVisitDeparting;
            if (visit.isOrder != expectsOrder)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has invalid order lifecycle.");
            if (visit.isInteractable != visit.isCustomerVisitLoading)
                throw new InvalidOperationException(
                    $"Customer vehicle {visit.EntityId} must be interactable only while loading.");
            if (visit.hasServingOrderCounterEntityId != visit.isCustomerVisitConsulting)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has invalid counter ownership.");
            if (visit.isCustomerVisitConsulting)
                ValidateCounterRelation(store, visit);

            bool vehicleMoving = visit.isCustomerVisitArriving ||
                                 visit.isCustomerVisitMovingToLoadingBay ||
                                 visit.isCustomerVisitDeparting;
            if (visit.hasRoute != vehicleMoving ||
                visit.hasRouteWaypointIndex != vehicleMoving || visit.isRouteCompleted)
            {
                throw new InvalidOperationException(
                    $"Customer vehicle {visit.EntityId} has invalid route state.");
            }

            GameEntity actor =
                _gameContext.GetEntityWithCustomerActorVisitEntityId(visit.EntityId);
            bool expectsActor = visit.isCustomerVisitQueued ||
                                visit.isCustomerVisitConsulting ||
                                visit.isCustomerVisitReturning;
            if ((actor != null) != expectsActor)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has invalid actor lifetime.");
            if (actor != null)
                ValidateActor(visit, actor);
        }

        private void ValidateActor(GameEntity visit, GameEntity actor)
        {
            if (actor.isDestructed || !actor.isCustomer || !actor.isRouteMover ||
                !actor.hasEntityId || !actor.hasCustomerActorVisitEntityId ||
                actor.CustomerActorVisitEntityId != visit.EntityId)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid actor.");
            }
            if (visit.isCustomerVisitQueued)
            {
                if (!actor.hasReservedCustomerQueueSpotEntityId ||
                    (actor.isCustomerApproachingCounter ? 1 : 0) +
                    (actor.isCustomerWaitingInQueue ? 1 : 0) != 1)
                {
                    throw new InvalidOperationException(
                        $"Queued customer {actor.EntityId} has invalid queue state.");
                }
                ValidateQueueRelation(visit, actor);
                return;
            }
            if (visit.isCustomerVisitConsulting)
            {
                if (!actor.hasReservedCustomerQueueSpotEntityId ||
                    !actor.isCustomerWaitingAtCounter)
                {
                    throw new InvalidOperationException(
                        $"Consulting customer {actor.EntityId} has invalid counter state.");
                }
                ValidateQueueRelation(visit, actor);
                return;
            }
            if (actor.hasReservedCustomerQueueSpotEntityId ||
                !actor.isCustomerReturningToVehicle ||
                !actor.hasRoute || !actor.hasRouteWaypointIndex || actor.isRouteCompleted)
            {
                throw new InvalidOperationException(
                    $"Returning customer {actor.EntityId} has invalid route state.");
            }
        }

        private void ValidateFifoQueue(GameEntity store)
        {
            _queueVisits.Sort((left, right) =>
                left.CustomerArrivalSequence.CompareTo(right.CustomerArrivalSequence));
            for (int expectedIndex = 0; expectedIndex < _queueVisits.Count; expectedIndex++)
            {
                GameEntity visit = _queueVisits[expectedIndex];
                GameEntity actor =
                    _gameContext.GetEntityWithCustomerActorVisitEntityId(visit.EntityId);
                GameEntity spot = _gameContext.GetEntityWithEntityId(
                    actor.ReservedCustomerQueueSpotEntityId);
                if (spot == null || spot.isDestructed || !spot.isCustomerQueueSpot ||
                    !spot.hasCustomerQueueSpotStoreEntityId ||
                    !spot.hasQueueSpotIndex ||
                    spot.CustomerQueueSpotStoreEntityId != store.EntityId ||
                    spot.QueueSpotIndex != expectedIndex)
                {
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} customer queue violates FIFO at index " +
                        $"{expectedIndex}.");
                }
            }
        }

        private void ValidateParkingRelation(GameEntity store, GameEntity visit)
        {
            GameEntity parkingSpot = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerParkingSpotEntityId);
            if (parkingSpot == null || parkingSpot.isDestructed ||
                !parkingSpot.isCustomerParkingSpot ||
                !parkingSpot.hasCustomerParkingSpotStoreEntityId ||
                parkingSpot.CustomerParkingSpotStoreEntityId != store.EntityId ||
                _gameContext.GetEntityWithReservedCustomerParkingSpotEntityId(
                    parkingSpot.EntityId) != visit)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} references an invalid parking spot.");
            }
        }

        private void ValidateLaneRelation(GameEntity store, GameEntity visit)
        {
            GameEntity lane = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerTrafficLaneEntityId);
            if (lane == null || lane.isDestructed || !lane.isCustomerTrafficLane ||
                !lane.hasCustomerTrafficLaneStoreEntityId ||
                lane.CustomerTrafficLaneStoreEntityId != store.EntityId ||
                _gameContext.GetEntityWithReservedCustomerTrafficLaneEntityId(
                    lane.EntityId) != visit)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} references an invalid traffic lane.");
            }
        }

        private void ValidateBayRelation(GameEntity store, GameEntity visit)
        {
            GameEntity bay = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerLoadingBayEntityId);
            if (bay == null || bay.isDestructed || !bay.isCustomerLoadingBay ||
                !bay.hasCustomerLoadingBayStoreEntityId ||
                bay.CustomerLoadingBayStoreEntityId != store.EntityId ||
                _gameContext.GetEntityWithReservedCustomerLoadingBayEntityId(
                    bay.EntityId) != visit)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} references an invalid loading bay.");
            }
        }

        private void ValidateCounterRelation(GameEntity store, GameEntity visit)
        {
            GameEntity counter = _gameContext.GetEntityWithEntityId(
                visit.ServingOrderCounterEntityId);
            if (counter == null || counter.isDestructed || !counter.isOrderCounter ||
                !counter.hasStoreEntityId || counter.StoreEntityId != store.EntityId ||
                !store.hasOrderCounterEntityId ||
                store.OrderCounterEntityId != counter.EntityId ||
                _gameContext.GetEntityWithServingOrderCounterEntityId(
                    counter.EntityId) != visit)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} references an invalid order counter.");
            }
        }

        private void ValidateQueueRelation(GameEntity visit, GameEntity actor)
        {
            GameEntity queueSpot = _gameContext.GetEntityWithEntityId(
                actor.ReservedCustomerQueueSpotEntityId);
            if (queueSpot == null || queueSpot.isDestructed ||
                !queueSpot.isCustomerQueueSpot ||
                !queueSpot.hasCustomerQueueSpotStoreEntityId ||
                queueSpot.CustomerQueueSpotStoreEntityId !=
                visit.CustomerVisitStoreEntityId ||
                _gameContext.GetEntityWithReservedCustomerQueueSpotEntityId(
                    queueSpot.EntityId) != actor)
            {
                throw new InvalidOperationException(
                    $"Customer {actor.EntityId} references an invalid queue spot.");
            }
        }

        private void ValidateBay(GameEntity store)
        {
            GameEntity bay =
                _gameContext.GetEntityWithCustomerLoadingBayStoreEntityId(store.EntityId);
            if (bay == null || bay.isDestructed || !bay.isCustomerLoadingBay ||
                !bay.hasEntityId || !bay.hasCustomerLoadingDepartureRoute)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid customer loading bay.");
            }
        }

        private void ValidateTrafficLane(GameEntity store)
        {
            GameEntity lane =
                _gameContext.GetEntityWithCustomerTrafficLaneStoreEntityId(store.EntityId);
            if (lane == null || lane.isDestructed || !lane.isCustomerTrafficLane ||
                !lane.hasEntityId)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid customer traffic lane.");
            }
        }
    }
}
