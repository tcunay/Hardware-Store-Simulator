using System;
using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class CustomerVisitFactory : ICustomerVisitFactory
    {
        private const float RouteRotationContinuityTolerance = 0.1f;

        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;
        private readonly IOrderFactory _orderFactory;

        public CustomerVisitFactory(IIdentifierService identifiers, IStaticDataService staticData,
            IOrderFactory orderFactory)
        {
            _identifiers = identifiers;
            _staticData = staticData;
            _orderFactory = orderFactory;
        }

        public GameEntity Create(GameEntity store, Pose[] arrivalRoute, Pose[] departureRoute)
        {
            ValidateStore(store);

            CustomerVehicleConfig config = _staticData.CustomerVehicle;
            Pose[] arrival = CloneAndValidateRoute(arrivalRoute, nameof(arrivalRoute));
            Pose[] departure = CloneAndValidateRoute(departureRoute, nameof(departureRoute));
            ValidateRouteContinuity(arrival, departure, config.WaypointTolerance);

            GameEntity customerVisit = CreateEntity.Empty(_identifiers.Next())
                .AddViewPrefab(config.ViewPrefab)
                .AddSpawnPosition(arrival[0].position)
                .AddSpawnRotation(arrival[0].rotation)
                .AddCustomerVisitStoreEntityId(store.EntityId)
                .AddRoute(arrival)
                .AddDepartureRoute(departure)
                .AddRouteWaypointIndex(1)
                .AddMovementSpeed(config.ArrivalSpeed)
                .AddRotationSpeed(config.RotationSpeed)
                .AddWaypointTolerance(config.WaypointTolerance)
                .With(x => x.isCustomerVisit = true)
                .With(x => x.isCustomerVehicle = true)
                .With(x => x.isCustomerVisitArriving = true)
                .With(x => x.isLoadingZone = true);

            _orderFactory.AddOrderComponents(
                customerVisit,
                store.StorageZoneEntityId);
            store.RemoveCustomerCooldownRemaining();
            return customerVisit;
        }

        private static void ValidateStore(GameEntity store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (!store.isStore || !store.hasEntityId || !store.hasStorageZoneEntityId)
                throw new InvalidOperationException("A customer visit requires a configured store.");
            if (!store.hasCustomerCooldownRemaining)
                throw new InvalidOperationException(
                    $"Store {store.EntityId} cannot start a customer visit outside cooldown.");
        }

        private static Pose[] CloneAndValidateRoute(Pose[] route, string argumentName)
        {
            if (route == null)
                throw new ArgumentNullException(argumentName);
            if (route.Length < 2)
                throw new ArgumentException(
                    "A customer vehicle route must contain at least two waypoints.",
                    argumentName);

            return (Pose[])route.Clone();
        }

        private static void ValidateRouteContinuity(Pose[] arrival, Pose[] departure,
            float tolerance)
        {
            float positionGap = Vector3.Distance(
                arrival[^1].position,
                departure[0].position);
            float rotationGap = Quaternion.Angle(
                arrival[^1].rotation,
                departure[0].rotation);
            if (positionGap > tolerance ||
                rotationGap > RouteRotationContinuityTolerance)
            {
                throw new InvalidOperationException(
                    "The customer departure route must start at the complete pose at the end " +
                    $"of the arrival route. Position gap: {positionGap}, rotation gap: {rotationGap}.");
            }
        }
    }
}
