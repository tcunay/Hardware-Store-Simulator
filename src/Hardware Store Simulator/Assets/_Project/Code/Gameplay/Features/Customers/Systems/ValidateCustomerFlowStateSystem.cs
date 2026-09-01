using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Physics;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class ValidateCustomerFlowStateSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly CustomerFlowConfig _config;
        private readonly IGroup<GameEntity> _stores;
        private readonly List<GameEntity> _storeBuffer = new(1);
        private readonly List<GameEntity> _queueVisits = new(4);
        private readonly List<GameEntity> _queueSpots = new(4);
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
            if (store.NextCustomerArrivalSequence < 0 ||
                !store.hasDayLostCustomerCount || store.DayLostCustomerCount < 0)
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has invalid customer-day state.");
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
            ValidateQueueSpots(store);
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
                !visit.isCustomerVehicle || !visit.isVehicleTrafficControlled ||
                visit.isRouteMover ||
                !visit.isLoadingZone || !visit.hasEntityId ||
                !visit.hasCustomerVisitStoreEntityId ||
                !visit.hasCustomerArrivalSequence ||
                !visit.hasVehicleTrafficCommandSequence ||
                visit.VehicleTrafficCommandSequence <= 0 ||
                visit.CustomerVisitStoreEntityId != store.EntityId ||
                visit.CustomerArrivalSequence < 0 ||
                visit.CustomerArrivalSequence >= store.NextCustomerArrivalSequence)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid customer visit.");
            }
            bool viewBindingPending = !visit.hasView && !visit.hasViewPrefab &&
                                      visit.hasSpawnPosition &&
                                      visit.hasSpawnRotation &&
                                      visit.isVehicleTrafficSpawnPending &&
                                      !visit.isVehicleTrafficReady &&
                                      !visit.hasVehicleTrafficRuntimeId;
            bool viewBound = visit.hasView && !visit.hasViewPrefab &&
                             !visit.hasSpawnPosition &&
                             !visit.hasSpawnRotation &&
                             !visit.isVehicleTrafficSpawnPending &&
                             visit.isVehicleTrafficReady &&
                             visit.hasVehicleTrafficRuntimeId &&
                             visit.VehicleTrafficRuntimeId >= 0;
            if (!viewBindingPending && !viewBound)
            {
                throw new InvalidOperationException(
                    $"Customer vehicle {visit.EntityId} has an invalid view-binding " +
                    $"state: hasView={visit.hasView}, " +
                    $"hasViewPrefab={visit.hasViewPrefab}, " +
                    $"hasSpawnPosition={visit.hasSpawnPosition}, " +
                    $"hasSpawnRotation={visit.hasSpawnRotation}.");
            }
            if (viewBindingPending)
            {
                if (visit.hasTransform || visit.hasRigidbody || visit.hasColliders)
                {
                    throw new InvalidOperationException(
                        $"Pending customer vehicle {visit.EntityId} has a partial view " +
                        $"binding: hasTransform={visit.hasTransform}, " +
                        $"hasRigidbody={visit.hasRigidbody}, " +
                        $"hasColliders={visit.hasColliders}.");
                }
            }
            else
            {
                ValidateVehiclePhysics(visit, usesTrafficProvider: true);
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
                (visit.isCustomerVisitDeparting ? 1 : 0) +
                (visit.isCustomerVisitAbandoning ? 1 : 0) +
                (visit.isCustomerVisitWaitingForAbandonDeparture ? 1 : 0) +
                (visit.isCustomerVisitAbandonDeparting ? 1 : 0);
            if (lifecycleCount != 1)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} must have exactly one lifecycle state.");

            bool expectsParking = visit.isCustomerVisitArriving ||
                                  visit.isCustomerVisitQueued ||
                                  visit.isCustomerVisitConsulting ||
                                  visit.isCustomerVisitReturning ||
                                  visit.isCustomerVisitWaitingForLoadingBay ||
                                  visit.isCustomerVisitMovingToLoadingBay ||
                                  visit.isCustomerVisitAbandoning ||
                                  visit.isCustomerVisitWaitingForAbandonDeparture ||
                                  visit.isCustomerVisitAbandonDeparting;
            if (visit.hasReservedCustomerParkingSpotEntityId != expectsParking)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has invalid parking ownership.");
            if (expectsParking)
                ValidateParkingRelation(store, visit);

            bool expectsLane = visit.isCustomerVisitArriving ||
                               visit.isCustomerVisitMovingToLoadingBay ||
                               visit.isCustomerVisitDeparting ||
                               visit.isCustomerVisitAbandonDeparting;
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
            ValidatePatience(visit);
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
                                 visit.isCustomerVisitDeparting ||
                                 visit.isCustomerVisitAbandonDeparting;
            if (visit.hasRoute != vehicleMoving ||
                visit.hasRouteWaypointIndex != vehicleMoving || visit.isRouteCompleted)
            {
                throw new InvalidOperationException(
                    $"Customer vehicle {visit.EntityId} has invalid route state.");
            }
            bool providerIsExecutingMove = visit.isVehicleTrafficSpawnPending ||
                                           visit.isVehicleTrafficMoving;
            if (providerIsExecutingMove != vehicleMoving ||
                (visit.isVehicleTrafficMoving && !visit.isVehicleTrafficReady))
            {
                throw new InvalidOperationException(
                    $"Customer vehicle {visit.EntityId} has invalid provider movement " +
                    $"state: moving={visit.isVehicleTrafficMoving}, " +
                    $"spawnPending={visit.isVehicleTrafficSpawnPending}, " +
                    $"lifecycleMoving={vehicleMoving}.");
            }

            GameEntity actor =
                _gameContext.GetEntityWithCustomerActorVisitEntityId(visit.EntityId);
            bool expectsActor = visit.isCustomerVisitQueued ||
                                visit.isCustomerVisitConsulting ||
                                visit.isCustomerVisitReturning ||
                                visit.isCustomerVisitAbandoning;
            if ((actor != null) != expectsActor)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has invalid actor lifetime.");
            if (actor != null)
                ValidateActor(visit, actor);
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static void ValidateVehiclePhysics(GameEntity visit,
            bool usesTrafficProvider)
        {
            if (!visit.hasTransform || !visit.hasRigidbody ||
                !visit.hasColliders)
            {
                throw new InvalidOperationException(
                    $"Bound customer vehicle {visit.EntityId} is missing registered " +
                    $"physics data: hasTransform={visit.hasTransform}, " +
                    $"hasRigidbody={visit.hasRigidbody}, " +
                    $"hasColliders={visit.hasColliders}.");
            }

            Rigidbody body = visit.Rigidbody;
            if (body.isKinematic || !body.useGravity || !body.detectCollisions ||
                body.interpolation != RigidbodyInterpolation.Interpolate ||
                body.collisionDetectionMode !=
                CollisionDetectionMode.ContinuousDynamic ||
                !IsFinite(body.linearVelocity) ||
                !IsFinite(body.angularVelocity))
            {
                throw new InvalidOperationException(
                    $"Customer vehicle {visit.EntityId} has an invalid dynamic physics " +
                    $"configuration: isKinematic={body.isKinematic}, " +
                    $"useGravity={body.useGravity}, " +
                    $"detectCollisions={body.detectCollisions}, " +
                    $"interpolation={body.interpolation}, " +
                    $"collisionDetection={body.collisionDetectionMode}, " +
                    $"constraints={body.constraints}, " +
                    $"linearVelocity={body.linearVelocity}, " +
                    $"angularVelocity={body.angularVelocity}.");
            }

            if (usesTrafficProvider)
                return;

            int solidColliderCount = 0;
            PhysicsMaterial hullMaterial = null;
            foreach (Collider collider in visit.Colliders)
            {
                if (collider == null)
                {
                    throw new InvalidOperationException(
                        $"Customer vehicle {visit.EntityId} has a missing collider.");
                }
                if (!collider.enabled || collider.isTrigger)
                    continue;

                solidColliderCount++;
                hullMaterial = collider.sharedMaterial;
            }
            if (solidColliderCount != 1 || hullMaterial == null ||
                hullMaterial.dynamicFriction != 0f ||
                hullMaterial.staticFriction != 0f ||
                hullMaterial.frictionCombine != PhysicsMaterialCombine.Minimum)
            {
                throw new InvalidOperationException(
                    $"Customer vehicle {visit.EntityId} requires one solid hull with " +
                    "a zero-friction physics material so its route motor can overcome " +
                    $"road contact: solidColliders={solidColliderCount}, " +
                    $"material={hullMaterial?.name ?? "missing"}.");
            }
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
            bool viewBindingPending = !actor.hasView && actor.hasViewPrefab &&
                                      actor.hasSpawnPosition &&
                                      actor.hasSpawnRotation;
            bool viewBound = actor.hasView && actor.hasViewPrefab &&
                             !actor.hasSpawnPosition &&
                             !actor.hasSpawnRotation;
            if (!viewBindingPending && !viewBound)
            {
                throw new InvalidOperationException(
                    $"Customer actor {actor.EntityId} has an invalid view-binding state.");
            }
            if (viewBindingPending)
            {
                if (actor.hasTransform || actor.hasRigidbody || actor.hasColliders)
                {
                    throw new InvalidOperationException(
                        $"Pending customer actor {actor.EntityId} has a partial view binding.");
                }
            }
            else
            {
                ValidateActorPhysics(actor);
            }

            int actorLifecycleCount =
                (actor.isCustomerApproachingCounter ? 1 : 0) +
                (actor.isCustomerWaitingInQueue ? 1 : 0) +
                (actor.isCustomerWaitingAtCounter ? 1 : 0) +
                (actor.isCustomerReturningToVehicle ? 1 : 0) +
                (actor.isCustomerAbandonReturningToVehicle ? 1 : 0);
            if (actorLifecycleCount != 1)
                throw new InvalidOperationException(
                    $"Customer actor {actor.EntityId} must have exactly one lifecycle state.");
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
            if (visit.isCustomerVisitAbandoning)
            {
                if (actor.hasReservedCustomerQueueSpotEntityId ||
                    !actor.isCustomerAbandonReturningToVehicle ||
                    actor.isCustomerReturningToVehicle ||
                    actor.hasCustomerReturnRoute ||
                    !actor.hasRoute || !actor.hasRouteWaypointIndex ||
                    actor.isRouteCompleted)
                {
                    throw new InvalidOperationException(
                        $"Abandoning customer {actor.EntityId} has invalid return state.");
                }
                return;
            }
            if (actor.hasReservedCustomerQueueSpotEntityId ||
                !actor.isCustomerReturningToVehicle ||
                actor.isCustomerAbandonReturningToVehicle ||
                !actor.hasRoute || !actor.hasRouteWaypointIndex || actor.isRouteCompleted)
            {
                throw new InvalidOperationException(
                    $"Returning customer {actor.EntityId} has invalid route state.");
            }
        }

        private static void ValidateActorPhysics(GameEntity actor)
        {
            if (!actor.hasTransform || !actor.hasRigidbody || !actor.hasColliders ||
                actor.Colliders == null || actor.Colliders.Length == 0 ||
                !actor.hasTrafficControlPolicy ||
                actor.TrafficControlPolicy !=
                TrafficControlPolicyId.Uncontrolled)
            {
                throw new InvalidOperationException(
                    $"Bound customer actor {actor.EntityId} is missing registered " +
                    "physics data.");
            }

            Rigidbody body = actor.Rigidbody;
            if (!body.isKinematic || body.useGravity || !body.detectCollisions ||
                body.interpolation != RigidbodyInterpolation.None ||
                body.collisionDetectionMode !=
                CollisionDetectionMode.ContinuousSpeculative)
            {
                throw new InvalidOperationException(
                    $"Customer actor {actor.EntityId} has an invalid route physics " +
                    "configuration.");
            }

            GhostMoverCollisionProfile.Validate(
                body,
                actor.Colliders,
                GhostMoverCollisionProfile.GhostMover);
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

        private void ValidateQueueSpots(GameEntity store)
        {
            _queueSpots.Clear();
            foreach (GameEntity spot in
                     _gameContext.GetEntitiesWithCustomerQueueSpotStoreEntityId(
                         store.EntityId))
            {
                if (spot == null || spot.isDestructed || !spot.isCustomerQueueSpot ||
                    !spot.hasEntityId || !spot.hasQueueSpotIndex ||
                    !spot.hasWorldPosition || !spot.hasWorldRotation ||
                    !spot.hasCustomerQueueAbandonRoute)
                {
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} owns an invalid queue spot.");
                }
                _queueSpots.Add(spot);
            }
            _queueSpots.Sort((left, right) =>
                left.QueueSpotIndex.CompareTo(right.QueueSpotIndex));

            Vector3 expectedExitOffset = default;
            for (int index = 0; index < _queueSpots.Count; index++)
            {
                GameEntity spot = _queueSpots[index];
                if (spot.QueueSpotIndex != index)
                {
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} has an invalid queue spot at index {index}.");
                }

                Pose[] route = spot.CustomerQueueAbandonRoute;
                int expectedLength = _config.ParkingCapacity - index + 1;
                if (route == null || route.Length != expectedLength)
                {
                    throw new InvalidOperationException(
                        $"Queue spot {spot.EntityId} must own an authored abandon-route " +
                        $"slice of length {expectedLength}.");
                }
                for (int routeIndex = 0; routeIndex < route.Length; routeIndex++)
                {
                    if (!IsFinite(route[routeIndex]))
                    {
                        throw new InvalidOperationException(
                            $"Queue spot {spot.EntityId} has a non-finite abandon-route " +
                            $"pose {routeIndex}.");
                    }
                }

                Vector3 exitOffset = route[0].position - spot.WorldPosition;
                if (exitOffset.sqrMagnitude < 0.000001f ||
                    Mathf.Abs(exitOffset.y) > 0.05f ||
                    (index > 0 &&
                     Vector3.Distance(exitOffset, expectedExitOffset) > 0.05f))
                {
                    throw new InvalidOperationException(
                        $"Queue spot {spot.EntityId} has a misaligned abandon exit.");
                }
                if (index == 0)
                    expectedExitOffset = exitOffset;

                if (index == 0)
                    continue;
                Pose[] previousRoute =
                    _queueSpots[index - 1].CustomerQueueAbandonRoute;
                for (int routeIndex = 0; routeIndex < route.Length; routeIndex++)
                {
                    if (!Matches(previousRoute[routeIndex + 1], route[routeIndex]))
                    {
                        throw new InvalidOperationException(
                            $"Queue spot {spot.EntityId} does not own the expected nested " +
                            "abandon-route slice.");
                    }
                }
            }
        }

        private static bool IsFinite(Pose pose) =>
            IsFinite(pose.position.x) && IsFinite(pose.position.y) &&
            IsFinite(pose.position.z) && IsFinite(pose.rotation.x) &&
            IsFinite(pose.rotation.y) && IsFinite(pose.rotation.z) &&
            IsFinite(pose.rotation.w);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool Matches(Pose first, Pose second) =>
            Vector3.Distance(first.position, second.position) <= 0.05f &&
            Quaternion.Angle(first.rotation, second.rotation) <= 0.1f;

        private void ValidateParkingRelation(GameEntity store, GameEntity visit)
        {
            GameEntity parkingSpot = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerParkingSpotEntityId);
            if (parkingSpot == null || parkingSpot.isDestructed ||
                !parkingSpot.isCustomerParkingSpot ||
                !parkingSpot.hasCustomerParkingSpotStoreEntityId ||
                !parkingSpot.hasCustomerVehicleParkingDepartureRoute ||
                parkingSpot.CustomerVehicleParkingDepartureRoute == null ||
                parkingSpot.CustomerVehicleParkingDepartureRoute.Length < 2 ||
                parkingSpot.CustomerParkingSpotStoreEntityId != store.EntityId ||
                _gameContext.GetEntityWithReservedCustomerParkingSpotEntityId(
                    parkingSpot.EntityId) != visit)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} references an invalid parking spot.");
            }
        }

        private void ValidatePatience(GameEntity visit)
        {
            bool expectsPatience = visit.isCustomerVisitArriving ||
                                   visit.isCustomerVisitQueued ||
                                   visit.isCustomerVisitConsulting;
            if (visit.hasCustomerPatienceRemaining != expectsPatience)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has invalid patience ownership.");
            if (!expectsPatience)
            {
                if (visit.isCustomerPatienceWarningIssued)
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} retains a stale patience warning.");
                return;
            }

            float remaining = visit.CustomerPatienceRemaining;
            if (float.IsNaN(remaining) || float.IsInfinity(remaining) || remaining < 0f ||
                (visit.isCustomerVisitArriving &&
                 remaining != _config.DefaultPatienceDuration))
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has invalid patience {remaining}.");
            }
            if (visit.isCustomerPatienceWarningIssued &&
                (remaining <= 0f || remaining > _config.PatienceWarningThreshold))
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid patience warning state.");
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
                !lane.hasEntityId ||
                !lane.hasCustomerTrafficLaneStoreEntityId ||
                lane.CustomerTrafficLaneStoreEntityId != store.EntityId)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid customer traffic lane.");
            }
        }
    }
}
