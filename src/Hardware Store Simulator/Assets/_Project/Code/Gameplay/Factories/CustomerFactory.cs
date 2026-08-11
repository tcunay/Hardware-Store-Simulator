using System;
using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class CustomerFactory : ICustomerFactory
    {
        private const float RouteRotationContinuityTolerance = 0.1f;

        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;

        public CustomerFactory(IIdentifierService identifiers, IStaticDataService staticData)
        {
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public GameEntity Create(GameEntity customerVisit, Pose[] routeToCounter,
            Pose[] routeToVehicle)
        {
            ValidateCustomerVisit(customerVisit);

            CustomerConfig config = _staticData.Customer;
            Pose[] approach = CloneAndValidateRoute(routeToCounter, nameof(routeToCounter));
            Pose[] returning = CloneAndValidateRoute(routeToVehicle, nameof(routeToVehicle));
            ValidateRouteContinuity(
                approach,
                returning,
                config.WaypointTolerance);

            return CreateEntity.Empty(_identifiers.Next())
                .AddViewPrefab(config.ViewPrefab)
                .AddSpawnPosition(approach[0].position)
                .AddSpawnRotation(approach[0].rotation)
                .AddCustomerActorVisitEntityId(customerVisit.EntityId)
                .AddRoute(approach)
                .AddCustomerReturnRoute(returning)
                .AddRouteWaypointIndex(1)
                .AddMovementSpeed(config.MovementSpeed)
                .AddRotationSpeed(config.RotationSpeed)
                .AddWaypointTolerance(config.WaypointTolerance)
                .With(x => x.isCustomer = true)
                .With(x => x.isCustomerApproachingCounter = true)
                .With(x => x.isRouteMover = true);
        }

        private static void ValidateCustomerVisit(GameEntity customerVisit)
        {
            if (customerVisit == null)
                throw new ArgumentNullException(nameof(customerVisit));
            if (!customerVisit.isCustomerVisit || !customerVisit.isCustomerVehicle ||
                !customerVisit.isCustomerVisitArriving || !customerVisit.hasEntityId ||
                !customerVisit.hasRoute || !customerVisit.hasRouteWaypointIndex ||
                !customerVisit.isRouteCompleted || customerVisit.isDestructed)
            {
                throw new InvalidOperationException(
                    "A customer actor requires a parked arriving customer visit.");
            }
        }

        private static Pose[] CloneAndValidateRoute(Pose[] route, string argumentName)
        {
            if (route == null)
                throw new ArgumentNullException(argumentName);
            if (route.Length < 2)
                throw new ArgumentException(
                    "A customer walking route must contain at least two waypoints.",
                    argumentName);

            return (Pose[])route.Clone();
        }

        private static void ValidateRouteContinuity(Pose[] approach, Pose[] returning,
            float tolerance)
        {
            float positionGap = Vector3.Distance(
                approach[^1].position,
                returning[0].position);
            float rotationGap = Quaternion.Angle(
                approach[^1].rotation,
                returning[0].rotation);
            if (positionGap <= tolerance &&
                rotationGap <= RouteRotationContinuityTolerance)
            {
                float vehicleDoorGap = Vector3.Distance(
                    returning[^1].position,
                    approach[0].position);
                if (vehicleDoorGap <= tolerance)
                    return;

                throw new InvalidOperationException(
                    "The customer return route must end at the same vehicle door where the " +
                    $"approach route starts. Position gap: {vehicleDoorGap}.");
            }

            throw new InvalidOperationException(
                "The customer return route must start at the complete pose at the end of " +
                $"the approach route. Position gap: {positionGap}, rotation gap: {rotationGap}.");
        }
    }
}
