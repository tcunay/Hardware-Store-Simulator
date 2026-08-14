using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class CompleteCustomerVehicleArrivalSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly ICustomerFactory _customerFactory;
        private readonly CustomerVehicleConfig _vehicleConfig;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _visitBuffer = new(4);
        private readonly List<GameEntity> _queueSpotBuffer = new(4);

        public CompleteCustomerVehicleArrivalSystem(GameContext gameContext,
            ICustomerFactory customerFactory, IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _customerFactory = customerFactory;
            _vehicleConfig = staticData.CustomerVehicle;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.CustomerVisitArriving,
                    GameMatcher.EntityId,
                    GameMatcher.CustomerVisitStoreEntityId,
                    GameMatcher.ReservedCustomerParkingSpotEntityId,
                    GameMatcher.ReservedCustomerTrafficLaneEntityId,
                    GameMatcher.LoadingZone,
                    GameMatcher.Slots,
                    GameMatcher.Route,
                    GameMatcher.RouteWaypointIndex,
                    GameMatcher.RouteCompleted)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_visitBuffer))
                CompleteArrival(visit);
        }

        private void CompleteArrival(GameEntity visit)
        {
            if (visit.Slots.Length != _vehicleConfig.CargoCapacity)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} exposes {visit.Slots.Length} loading " +
                    $"slots, but {nameof(CustomerVehicleConfig)} requires " +
                    $"{_vehicleConfig.CargoCapacity}.");
            }
            if (_gameContext.GetEntityWithCustomerActorVisitEntityId(visit.EntityId) != null)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} already has a customer actor.");

            GameEntity parkingSpot = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerParkingSpotEntityId);
            ValidateParkingSpot(visit, parkingSpot);
            GameEntity trafficLane = _gameContext.GetEntityWithEntityId(
                visit.ReservedCustomerTrafficLaneEntityId);
            ValidateTrafficLane(visit, trafficLane);
            GameEntity queueSpot = FindQueueTail(visit);

            visit.isRouteCompleted = false;
            visit.RemoveRoute();
            visit.RemoveRouteWaypointIndex();
            visit.RemoveReservedCustomerTrafficLaneEntityId();
            visit.isCustomerVisitArriving = false;
            visit.isCustomerVisitQueued = true;
            _customerFactory.Create(visit, parkingSpot, queueSpot);
        }

        private GameEntity FindQueueTail(GameEntity visit)
        {
            _queueSpotBuffer.Clear();
            foreach (GameEntity queueSpot in
                     _gameContext.GetEntitiesWithCustomerQueueSpotStoreEntityId(
                         visit.CustomerVisitStoreEntityId))
            {
                if (queueSpot == null || queueSpot.isDestructed ||
                    !queueSpot.isCustomerQueueSpot || !queueSpot.hasEntityId ||
                    !queueSpot.hasQueueSpotIndex || !queueSpot.hasWorldPosition ||
                    !queueSpot.hasWorldRotation)
                {
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} found an invalid queue spot.");
                }
                _queueSpotBuffer.Add(queueSpot);
            }

            _queueSpotBuffer.Sort((left, right) =>
                left.QueueSpotIndex.CompareTo(right.QueueSpotIndex));
            int occupiedCount = 0;
            for (int index = 0; index < _queueSpotBuffer.Count; index++)
            {
                GameEntity queueSpot = _queueSpotBuffer[index];
                if (queueSpot.QueueSpotIndex != index)
                    throw new InvalidOperationException(
                        $"Store {visit.CustomerVisitStoreEntityId} queue indices must be " +
                        "contiguous.");
                if (_gameContext.GetEntityWithReservedCustomerQueueSpotEntityId(
                        queueSpot.EntityId) != null)
                {
                    occupiedCount++;
                }
            }

            if (occupiedCount >= _queueSpotBuffer.Count)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} parked without a free queue spot.");
            GameEntity tail = _queueSpotBuffer[occupiedCount];
            if (_gameContext.GetEntityWithReservedCustomerQueueSpotEntityId(
                    tail.EntityId) != null)
            {
                throw new InvalidOperationException(
                    $"Customer queue contains a gap before slot {occupiedCount}.");
            }

            return tail;
        }

        private static void ValidateParkingSpot(GameEntity visit, GameEntity parkingSpot)
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

        private static void ValidateTrafficLane(GameEntity visit, GameEntity trafficLane)
        {
            if (trafficLane == null || trafficLane.isDestructed ||
                !trafficLane.isCustomerTrafficLane || !trafficLane.hasEntityId ||
                !trafficLane.hasCustomerTrafficLaneStoreEntityId ||
                trafficLane.CustomerTrafficLaneStoreEntityId !=
                visit.CustomerVisitStoreEntityId)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid traffic-lane reservation.");
            }
        }
    }
}
