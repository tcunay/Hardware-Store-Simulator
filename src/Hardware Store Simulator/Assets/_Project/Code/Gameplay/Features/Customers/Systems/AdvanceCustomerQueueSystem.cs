using System;
using System.Collections.Generic;
using Entitas;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class AdvanceCustomerQueueSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _stores;
        private readonly List<GameEntity> _storeBuffer = new(1);
        private readonly List<GameEntity> _visits = new(4);
        private readonly List<GameEntity> _actors = new(4);
        private readonly List<GameEntity> _queueSpots = new(4);

        public AdvanceCustomerQueueSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _stores = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Store,
                    GameMatcher.EntityId)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity store in _stores.GetEntities(_storeBuffer))
                Advance(store);
        }

        private void Advance(GameEntity store)
        {
            CollectQueueSpots(store);
            CollectQueuedActors(store);
            for (int expectedIndex = 0; expectedIndex < _actors.Count; expectedIndex++)
            {
                GameEntity actor = _actors[expectedIndex];
                GameEntity currentSpot = _gameContext.GetEntityWithEntityId(
                    actor.ReservedCustomerQueueSpotEntityId);
                ValidateQueueSpot(store, currentSpot);
                int currentIndex = currentSpot.QueueSpotIndex;
                if (currentIndex == expectedIndex)
                    continue;
                if (currentIndex < expectedIndex)
                {
                    throw new InvalidOperationException(
                        $"Customer {actor.EntityId} queue position {currentIndex} violates " +
                        $"FIFO index {expectedIndex}.");
                }
                if (actor.isCustomerWaitingAtCounter)
                    throw new InvalidOperationException(
                        $"Customer {actor.EntityId} cannot move away from an occupied counter.");

                GameEntity targetSpot = _queueSpots[expectedIndex];
                if (_gameContext.GetEntityWithReservedCustomerQueueSpotEntityId(
                        targetSpot.EntityId) != null)
                {
                    throw new InvalidOperationException(
                        $"Customer queue slot {expectedIndex} is not free for FIFO advance.");
                }

                actor.RemoveReservedCustomerQueueSpotEntityId();
                actor.AddReservedCustomerQueueSpotEntityId(targetSpot.EntityId);
                BeginMove(actor, targetSpot);
            }
        }

        private void CollectQueueSpots(GameEntity store)
        {
            _queueSpots.Clear();
            foreach (GameEntity spot in
                     _gameContext.GetEntitiesWithCustomerQueueSpotStoreEntityId(
                         store.EntityId))
            {
                ValidateQueueSpot(store, spot);
                _queueSpots.Add(spot);
            }

            _queueSpots.Sort((left, right) =>
                left.QueueSpotIndex.CompareTo(right.QueueSpotIndex));
            for (int index = 0; index < _queueSpots.Count; index++)
            {
                if (_queueSpots[index].QueueSpotIndex != index)
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} queue indices must be contiguous.");
            }
        }

        private void CollectQueuedActors(GameEntity store)
        {
            _visits.Clear();
            foreach (GameEntity visit in
                     _gameContext.GetEntitiesWithCustomerVisitStoreEntityId(store.EntityId))
            {
                if (visit.isDestructed ||
                    (!visit.isCustomerVisitQueued &&
                     !visit.isCustomerVisitConsulting))
                {
                    continue;
                }
                if (!visit.isCustomerVisit || !visit.hasEntityId ||
                    !visit.hasCustomerArrivalSequence)
                {
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} has an invalid queued customer visit.");
                }
                _visits.Add(visit);
            }

            _visits.Sort((left, right) =>
                left.CustomerArrivalSequence.CompareTo(right.CustomerArrivalSequence));
            for (int index = 1; index < _visits.Count; index++)
            {
                if (_visits[index - 1].CustomerArrivalSequence ==
                    _visits[index].CustomerArrivalSequence)
                {
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} has duplicate queued customer arrival " +
                        $"sequence {_visits[index].CustomerArrivalSequence}.");
                }
            }
            _actors.Clear();
            foreach (GameEntity visit in _visits)
            {
                GameEntity actor =
                    _gameContext.GetEntityWithCustomerActorVisitEntityId(visit.EntityId);
                if (actor == null || actor.isDestructed || !actor.isCustomer ||
                    !actor.hasEntityId || !actor.hasReservedCustomerQueueSpotEntityId ||
                    !actor.hasTransform || !actor.hasRigidbody ||
                    !actor.hasMovementSpeed || !actor.hasRotationSpeed ||
                    !actor.hasWaypointTolerance)
                {
                    throw new InvalidOperationException(
                        $"Queued customer visit {visit.EntityId} has an invalid actor.");
                }
                if (!actor.isCustomerApproachingCounter &&
                    !actor.isCustomerWaitingInQueue &&
                    !actor.isCustomerWaitingAtCounter)
                {
                    throw new InvalidOperationException(
                        $"Queued customer {actor.EntityId} has no queue lifecycle state.");
                }
                _actors.Add(actor);
            }
            if (_actors.Count > _queueSpots.Count)
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has more queued customers than queue spots.");
        }

        private static void BeginMove(GameEntity actor, GameEntity targetSpot)
        {
            if (actor.hasRoute)
                actor.RemoveRoute();
            if (actor.hasRouteWaypointIndex)
                actor.RemoveRouteWaypointIndex();
            if (actor.isRouteCompleted)
                actor.isRouteCompleted = false;
            if (actor.isCustomerWaitingInQueue)
                actor.isCustomerWaitingInQueue = false;
            actor.isCustomerApproachingCounter = true;

            var route = new[]
            {
                new Pose(actor.Rigidbody.position, actor.Rigidbody.rotation),
                new Pose(targetSpot.WorldPosition, targetSpot.WorldRotation)
            };
            actor.AddRoute(route);
            actor.AddRouteWaypointIndex(1);
        }

        private static void ValidateQueueSpot(GameEntity store, GameEntity queueSpot)
        {
            if (queueSpot == null || queueSpot.isDestructed ||
                !queueSpot.isCustomerQueueSpot || !queueSpot.hasEntityId ||
                !queueSpot.hasCustomerQueueSpotStoreEntityId ||
                !queueSpot.hasQueueSpotIndex || !queueSpot.hasWorldPosition ||
                !queueSpot.hasWorldRotation ||
                queueSpot.CustomerQueueSpotStoreEntityId != store.EntityId)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid customer queue spot.");
            }
        }
    }
}
