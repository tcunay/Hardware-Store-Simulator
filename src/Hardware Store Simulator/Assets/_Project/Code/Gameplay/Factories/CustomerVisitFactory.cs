using System;
using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class CustomerVisitFactory : ICustomerVisitFactory
    {
        private readonly GameContext _gameContext;
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;
        private readonly IConsultationOfferFactory _consultationOffers;

        public CustomerVisitFactory(GameContext gameContext,
            IIdentifierService identifiers, IStaticDataService staticData,
            IConsultationOfferFactory consultationOffers)
        {
            _gameContext = gameContext;
            _identifiers = identifiers;
            _staticData = staticData;
            _consultationOffers = consultationOffers;
        }

        public GameEntity Create(GameEntity store, GameEntity parkingSpot,
            GameEntity trafficLane)
        {
            ValidateStore(store);
            ValidateParkingSpot(store, parkingSpot);
            ValidateTrafficLane(store, trafficLane);

            CustomerVehicleConfig config = _staticData.CustomerVehicle;
            Pose[] arrival = (Pose[])parkingSpot.CustomerVehicleArrivalRoute.Clone();
            int projectSequenceIndex = store.NextProjectSequenceIndex;
            if (projectSequenceIndex < 0 ||
                projectSequenceIndex >= _staticData.ProjectTypes.Count)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has invalid next project sequence index " +
                    $"{projectSequenceIndex} for {_staticData.ProjectTypes.Count} projects.");
            }
            if (store.NextCustomerArrivalSequence < 0)
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has invalid customer arrival sequence.");

            CustomerProjectTypeId projectType =
                _staticData.ProjectTypes[projectSequenceIndex];
            int arrivalSequence = store.NextCustomerArrivalSequence;
            GameEntity customerVisit = CreateEntity.Empty(_identifiers.Next())
                .AddViewPrefab(config.ViewPrefab)
                .AddSpawnPosition(arrival[0].position)
                .AddSpawnRotation(arrival[0].rotation)
                .AddCustomerVisitStoreEntityId(store.EntityId)
                .AddStorageZoneEntityId(store.StorageZoneEntityId)
                .AddCustomerProjectType(projectType)
                .AddCustomerArrivalSequence(arrivalSequence)
                .AddCustomerPatienceRemaining(
                    _staticData.CustomerFlow.DefaultPatienceDuration)
                .AddReservedCustomerParkingSpotEntityId(parkingSpot.EntityId)
                .AddReservedCustomerTrafficLaneEntityId(trafficLane.EntityId)
                .AddRoute(arrival)
                .AddRouteWaypointIndex(1)
                .AddMovementSpeed(config.ArrivalSpeed)
                .AddRotationSpeed(config.RotationSpeed)
                .AddWaypointTolerance(config.WaypointTolerance)
                .With(x => x.isCustomerVisit = true)
                .With(x => x.isCustomerVehicle = true)
                .With(x => x.isCustomerVisitArriving = true)
                .With(x => x.isRouteMover = true)
                .With(x => x.isLoadingZone = true);

            _consultationOffers.CreateOffers(customerVisit);
            store.ReplaceNextProjectSequenceIndex(
                (projectSequenceIndex + 1) % _staticData.ProjectTypes.Count);
            store.ReplaceNextCustomerArrivalSequence(checked(arrivalSequence + 1));
            return customerVisit;
        }

        private static void ValidateStore(GameEntity store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (!store.isStore || !store.isStoreOpen || !store.hasEntityId ||
                !store.hasStorageZoneEntityId || !store.hasNextProjectSequenceIndex ||
                !store.hasNextCustomerArrivalSequence ||
                !store.hasCustomerCooldownRemaining)
            {
                throw new InvalidOperationException(
                    "A customer visit requires a configured open store.");
            }
            float cooldown = store.CustomerCooldownRemaining;
            if (float.IsNaN(cooldown) || float.IsInfinity(cooldown) || cooldown != 0f)
                throw new InvalidOperationException(
                    $"Store {store.EntityId} cannot spawn a customer before cooldown expires.");
        }

        private void ValidateParkingSpot(GameEntity store, GameEntity parkingSpot)
        {
            if (parkingSpot == null || parkingSpot.isDestructed ||
                !parkingSpot.isCustomerParkingSpot || !parkingSpot.hasEntityId ||
                !parkingSpot.hasCustomerParkingSpotStoreEntityId ||
                !parkingSpot.hasParkingSpotIndex ||
                !parkingSpot.hasCustomerVehicleArrivalRoute ||
                !parkingSpot.hasCustomerVehicleToLoadingRoute ||
                !parkingSpot.hasCustomerVehicleParkingDepartureRoute ||
                !parkingSpot.hasCustomerApproachRoute ||
                !parkingSpot.hasCustomerReturnRoute ||
                parkingSpot.CustomerParkingSpotStoreEntityId != store.EntityId)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} cannot reserve an invalid parking spot.");
            }
            if (parkingSpot.CustomerVehicleArrivalRoute == null ||
                parkingSpot.CustomerVehicleArrivalRoute.Length < 2 ||
                parkingSpot.CustomerVehicleParkingDepartureRoute == null ||
                parkingSpot.CustomerVehicleParkingDepartureRoute.Length < 2 ||
                _gameContext.GetEntityWithReservedCustomerParkingSpotEntityId(
                    parkingSpot.EntityId) != null)
            {
                throw new InvalidOperationException(
                    $"Customer parking spot {parkingSpot.EntityId} is unavailable.");
            }
        }

        private void ValidateTrafficLane(GameEntity store, GameEntity trafficLane)
        {
            if (trafficLane == null || trafficLane.isDestructed ||
                !trafficLane.isCustomerTrafficLane || !trafficLane.hasEntityId ||
                !trafficLane.hasCustomerTrafficLaneStoreEntityId ||
                trafficLane.CustomerTrafficLaneStoreEntityId != store.EntityId ||
                _gameContext.GetEntityWithReservedCustomerTrafficLaneEntityId(
                    trafficLane.EntityId) != null)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} customer traffic lane is unavailable.");
            }
        }
    }
}
