using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Customers;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class SpawnCustomerVisitSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly ICustomerVisitFactory _customerVisitFactory;
        private readonly ICustomerArrivalSchedule _arrivalSchedule;
        private readonly IStaticDataService _staticData;
        private readonly IGroup<GameEntity> _stores;
        private readonly List<GameEntity> _storeBuffer = new(1);
        private readonly List<GameEntity> _parkingBuffer = new(4);

        public SpawnCustomerVisitSystem(GameContext gameContext,
            ICustomerVisitFactory customerVisitFactory,
            ICustomerArrivalSchedule arrivalSchedule,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _customerVisitFactory = customerVisitFactory;
            _arrivalSchedule = arrivalSchedule;
            _staticData = staticData;
            _stores = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Store,
                    GameMatcher.StoreOpen,
                    GameMatcher.EntityId,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.NextProjectSequenceIndex,
                    GameMatcher.NextCustomerArrivalSequence,
                    GameMatcher.CurrentDayMinute,
                    GameMatcher.CustomerCooldownRemaining,
                    GameMatcher.CustomerDemandProjectType,
                    GameMatcher.CustomerDemandOfferIndex)
                .NoneOf(
                    GameMatcher.CustomerDemandUnavailable,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity store in _stores.GetEntities(_storeBuffer))
            {
                ValidateCooldown(store);
                if (store.CustomerCooldownRemaining > 0f)
                    continue;
                int projectIndex = ValidateDemand(store);

                float nextDelay = _arrivalSchedule.GetDelay(store.CurrentDayMinute);

                GameEntity trafficLane =
                    _gameContext.GetEntityWithCustomerTrafficLaneStoreEntityId(
                        store.EntityId);
                ValidateTrafficLane(store, trafficLane);
                if (_gameContext.GetEntityWithReservedCustomerTrafficLaneEntityId(
                        trafficLane.EntityId) == null)
                {
                    GameEntity parkingSpot = FindAvailableParkingSpot(store);
                    if (parkingSpot != null)
                    {
                        int arrivalSequence = store.NextCustomerArrivalSequence;
                        _customerVisitFactory.Create(
                            store,
                            parkingSpot,
                            trafficLane,
                            store.CustomerDemandProjectType,
                            store.CustomerDemandOfferIndex,
                            arrivalSequence);
                        store.ReplaceNextProjectSequenceIndex(
                            (projectIndex + 1) % _staticData.ProjectTypes.Count);
                        store.ReplaceNextCustomerArrivalSequence(
                            checked(arrivalSequence + 1));
                    }
                }

                store.ReplaceCustomerCooldownRemaining(nextDelay);
            }
        }

        private int ValidateDemand(GameEntity store)
        {
            int projectIndex = -1;
            for (int index = 0; index < _staticData.ProjectTypes.Count; index++)
            {
                if (_staticData.ProjectTypes[index] !=
                    store.CustomerDemandProjectType)
                {
                    continue;
                }

                projectIndex = index;
                break;
            }

            if (projectIndex < 0)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} selected unknown customer project " +
                    $"{store.CustomerDemandProjectType}.");
            }

            CustomerProjectConfig project = _staticData.GetProject(
                store.CustomerDemandProjectType);
            if (store.CustomerDemandOfferIndex < 0 ||
                store.CustomerDemandOfferIndex >= project.Offers.Count)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} selected invalid offer " +
                    $"{store.CustomerDemandOfferIndex} for " +
                    $"{store.CustomerDemandProjectType}.");
            }
            if (store.NextCustomerArrivalSequence < 0)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has invalid customer arrival sequence.");
            }

            return projectIndex;
        }

        private GameEntity FindAvailableParkingSpot(GameEntity store)
        {
            _parkingBuffer.Clear();
            foreach (GameEntity parkingSpot in
                     _gameContext.GetEntitiesWithCustomerParkingSpotStoreEntityId(
                         store.EntityId))
            {
                ValidateParkingSpot(store, parkingSpot);
                _parkingBuffer.Add(parkingSpot);
            }

            _parkingBuffer.Sort((left, right) =>
                left.ParkingSpotIndex.CompareTo(right.ParkingSpotIndex));
            for (int index = 0; index < _parkingBuffer.Count; index++)
            {
                GameEntity parkingSpot = _parkingBuffer[index];
                if (parkingSpot.ParkingSpotIndex != index)
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} customer parking indices must be contiguous.");
                if (_gameContext.GetEntityWithReservedCustomerParkingSpotEntityId(
                        parkingSpot.EntityId) == null)
                {
                    return parkingSpot;
                }
            }

            return null;
        }

        private static void ValidateCooldown(GameEntity store)
        {
            float cooldown = store.CustomerCooldownRemaining;
            if (float.IsNaN(cooldown) || float.IsInfinity(cooldown) || cooldown < 0f)
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has invalid customer cooldown {cooldown}.");
        }

        private static void ValidateTrafficLane(GameEntity store, GameEntity trafficLane)
        {
            if (trafficLane == null || trafficLane.isDestructed ||
                !trafficLane.isCustomerTrafficLane || !trafficLane.hasEntityId ||
                !trafficLane.hasCustomerTrafficLaneStoreEntityId ||
                trafficLane.CustomerTrafficLaneStoreEntityId != store.EntityId)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has no valid customer traffic lane.");
            }
        }

        private static void ValidateParkingSpot(GameEntity store, GameEntity parkingSpot)
        {
            if (parkingSpot == null || parkingSpot.isDestructed ||
                !parkingSpot.isCustomerParkingSpot || !parkingSpot.hasEntityId ||
                !parkingSpot.hasCustomerParkingSpotStoreEntityId ||
                !parkingSpot.hasParkingSpotIndex ||
                !parkingSpot.hasCustomerVehicleArrivalRoute ||
                parkingSpot.CustomerParkingSpotStoreEntityId != store.EntityId)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid customer parking spot.");
            }
        }
    }
}
