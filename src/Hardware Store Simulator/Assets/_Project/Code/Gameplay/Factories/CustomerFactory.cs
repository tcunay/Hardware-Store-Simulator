using System;
using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class CustomerFactory : ICustomerFactory
    {
        private readonly GameContext _gameContext;
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;

        public CustomerFactory(GameContext gameContext,
            IIdentifierService identifiers, IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public GameEntity Create(GameEntity customerVisit, GameEntity parkingSpot,
            GameEntity queueSpot)
        {
            ValidateCustomerVisit(customerVisit, parkingSpot);
            ValidateQueueSpot(customerVisit, queueSpot);

            CustomerConfig config = _staticData.Customer;
            Pose[] approach = (Pose[])parkingSpot.CustomerApproachRoute.Clone();
            approach[^1] = new Pose(queueSpot.WorldPosition, queueSpot.WorldRotation);
            Pose[] returning = (Pose[])parkingSpot.CustomerReturnRoute.Clone();

            return CreateEntity.Empty(_identifiers.Next())
                .AddViewPrefab(config.ViewPrefab)
                .AddSpawnPosition(approach[0].position)
                .AddSpawnRotation(approach[0].rotation)
                .AddCustomerActorVisitEntityId(customerVisit.EntityId)
                .AddReservedCustomerQueueSpotEntityId(queueSpot.EntityId)
                .AddRoute(approach)
                .AddCustomerReturnRoute(returning)
                .AddRouteWaypointIndex(1)
                .AddMovementSpeed(config.MovementSpeed)
                .AddRotationSpeed(config.RotationSpeed)
                .AddWaypointTolerance(config.WaypointTolerance)
                .AddTrafficControlPolicy(TrafficControlPolicyId.Uncontrolled)
                .AddTrafficPriority((int)TrafficPriorityId.CustomerPedestrian)
                .AddTrafficDesiredVelocity(Vector3.zero)
                .AddTrafficIntentDistance(0f)
                .AddTrafficAngularIntent(0f)
                .AddTrafficPreviousPosition(approach[0].position)
                .AddTrafficCurrentSpeed(config.MovementSpeed)
                .With(x => x.isCustomer = true)
                .With(x => x.isTrafficParticipant = true)
                .With(x => x.isCustomerApproachingCounter = true)
                .With(x => x.isRouteMover = true);
        }

        private void ValidateCustomerVisit(GameEntity visit, GameEntity parkingSpot)
        {
            if (visit == null || !visit.isCustomerVisit || !visit.isCustomerVehicle ||
                !visit.isCustomerVisitQueued || !visit.hasEntityId ||
                !visit.hasCustomerPatienceRemaining ||
                !visit.hasReservedCustomerParkingSpotEntityId || visit.hasRoute ||
                visit.hasRouteWaypointIndex || visit.isRouteCompleted || visit.isDestructed)
            {
                throw new InvalidOperationException(
                    "A customer actor requires a parked queued customer visit.");
            }
            if (_gameContext.GetEntityWithCustomerActorVisitEntityId(visit.EntityId) != null)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} already has a customer actor.");
            if (parkingSpot == null || parkingSpot.isDestructed ||
                !parkingSpot.isCustomerParkingSpot || !parkingSpot.hasEntityId ||
                !parkingSpot.hasCustomerApproachRoute ||
                !parkingSpot.hasCustomerReturnRoute ||
                parkingSpot.EntityId != visit.ReservedCustomerParkingSpotEntityId ||
                parkingSpot.CustomerApproachRoute == null ||
                parkingSpot.CustomerApproachRoute.Length < 2 ||
                parkingSpot.CustomerReturnRoute == null ||
                parkingSpot.CustomerReturnRoute.Length < 2)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid parking relation.");
            }
        }

        private void ValidateQueueSpot(GameEntity visit, GameEntity queueSpot)
        {
            if (queueSpot == null || queueSpot.isDestructed ||
                !queueSpot.isCustomerQueueSpot || !queueSpot.hasEntityId ||
                !queueSpot.hasCustomerQueueSpotStoreEntityId ||
                !queueSpot.hasQueueSpotIndex || !queueSpot.hasWorldPosition ||
                !queueSpot.hasWorldRotation ||
                queueSpot.CustomerQueueSpotStoreEntityId !=
                visit.CustomerVisitStoreEntityId ||
                _gameContext.GetEntityWithReservedCustomerQueueSpotEntityId(
                    queueSpot.EntityId) != null)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} cannot reserve an invalid queue spot.");
            }
        }
    }
}
