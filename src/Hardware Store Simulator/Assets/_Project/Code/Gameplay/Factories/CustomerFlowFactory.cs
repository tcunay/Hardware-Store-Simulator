using System;
using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Scene;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class CustomerFlowFactory : ICustomerFlowFactory
    {
        private const float PositionTolerance = 0.05f;
        private const float RotationTolerance = 0.1f;

        private readonly GameContext _gameContext;
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;

        public CustomerFlowFactory(GameContext gameContext,
            IIdentifierService identifiers, IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public void Create(GameEntity store, CustomerFlowSceneLayout layout)
        {
            ValidateStore(store);
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));
            if (_gameContext.GetEntitiesWithCustomerParkingSpotStoreEntityId(
                    store.EntityId).Count != 0 ||
                _gameContext.GetEntitiesWithCustomerQueueSpotStoreEntityId(
                    store.EntityId).Count != 0 ||
                _gameContext.GetEntityWithCustomerLoadingBayStoreEntityId(
                    store.EntityId) != null ||
                _gameContext.GetEntityWithCustomerTrafficLaneStoreEntityId(
                    store.EntityId) != null)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} already owns customer-flow infrastructure.");
            }

            CustomerParkingSpotSceneLayout[] parkingSpots = layout.ParkingSpots;
            Pose[] queuePoses = layout.QueuePoses;
            Pose[] loadingDepartureRoute = layout.LoadingDepartureRoute;
            int capacity = _staticData.CustomerFlow.ParkingCapacity;
            if (parkingSpots == null || parkingSpots.Length != capacity)
                throw new InvalidOperationException(
                    $"Customer flow requires exactly {capacity} parking spots.");
            if (queuePoses == null || queuePoses.Length != capacity)
                throw new InvalidOperationException(
                    $"Customer flow requires exactly {capacity} queue spots.");
            Pose[] departure = CloneAndValidateRoute(
                loadingDepartureRoute,
                nameof(layout.LoadingDepartureRoute));
            ValidatePoses(queuePoses, nameof(layout.QueuePoses));

            var spotsByIndex = new CustomerParkingSpotSceneLayout[capacity];
            for (int index = 0; index < parkingSpots.Length; index++)
            {
                CustomerParkingSpotSceneLayout spot = parkingSpots[index] ??
                    throw new InvalidOperationException(
                        $"Customer parking layout {index} is missing.");
                if (spot.Index < 0 || spot.Index >= capacity ||
                    spotsByIndex[spot.Index] != null)
                {
                    throw new InvalidOperationException(
                        $"Customer parking spot index {spot.Index} must be unique in range " +
                        $"[0, {capacity - 1}].");
                }

                spotsByIndex[spot.Index] = spot;
            }

            for (int index = 0; index < capacity; index++)
                CreateParkingSpot(store, spotsByIndex[index], queuePoses, departure);

            for (int index = 0; index < queuePoses.Length; index++)
            {
                CreateEntity.Empty(_identifiers.Next())
                    .AddCustomerQueueSpotStoreEntityId(store.EntityId)
                    .AddQueueSpotIndex(index)
                    .AddWorldPosition(queuePoses[index].position)
                    .AddWorldRotation(queuePoses[index].rotation)
                    .With(x => x.isCustomerQueueSpot = true);
            }

            CreateEntity.Empty(_identifiers.Next())
                .AddCustomerLoadingBayStoreEntityId(store.EntityId)
                .AddCustomerLoadingDepartureRoute(departure)
                .With(x => x.isCustomerLoadingBay = true);

            CreateEntity.Empty(_identifiers.Next())
                .AddCustomerTrafficLaneStoreEntityId(store.EntityId)
                .With(x => x.isCustomerTrafficLane = true);
        }

        private void CreateParkingSpot(GameEntity store,
            CustomerParkingSpotSceneLayout spot, Pose[] queuePoses, Pose[] departure)
        {
            Pose[] arrival = CloneAndValidateRoute(
                spot.VehicleArrivalRoute,
                $"parking[{spot.Index}].{nameof(spot.VehicleArrivalRoute)}");
            Pose[] toLoading = CloneAndValidateRoute(
                spot.VehicleToLoadingRoute,
                $"parking[{spot.Index}].{nameof(spot.VehicleToLoadingRoute)}");
            Pose[] approach = CloneAndValidateRoute(
                spot.CustomerApproachRoute,
                $"parking[{spot.Index}].{nameof(spot.CustomerApproachRoute)}");
            Pose[] customerReturn = CloneAndValidateRoute(
                spot.CustomerReturnRoute,
                $"parking[{spot.Index}].{nameof(spot.CustomerReturnRoute)}");

            RequireContinuous(arrival[^1], toLoading[0],
                $"parking {spot.Index} arrival and loading routes");
            RequireContinuous(toLoading[^1], departure[0],
                $"parking {spot.Index} loading and departure routes");
            RequireContinuous(approach[^1], queuePoses[^1],
                $"parking {spot.Index} approach and queue tail");
            RequireContinuous(customerReturn[0], queuePoses[0],
                $"parking {spot.Index} return and queue head");
            RequireContinuous(approach[0], customerReturn[^1],
                $"parking {spot.Index} customer door routes");

            CreateEntity.Empty(_identifiers.Next())
                .AddCustomerParkingSpotStoreEntityId(store.EntityId)
                .AddParkingSpotIndex(spot.Index)
                .AddCustomerVehicleArrivalRoute(arrival)
                .AddCustomerVehicleToLoadingRoute(toLoading)
                .AddCustomerApproachRoute(approach)
                .AddCustomerReturnRoute(customerReturn)
                .With(x => x.isCustomerParkingSpot = true);
        }

        private static Pose[] CloneAndValidateRoute(Pose[] route, string owner)
        {
            if (route == null || route.Length < 2)
                throw new InvalidOperationException(
                    $"Customer route {owner} must contain at least two poses.");
            ValidatePoses(route, owner);
            return (Pose[])route.Clone();
        }

        private static void ValidatePoses(Pose[] poses, string owner)
        {
            for (int index = 0; index < poses.Length; index++)
            {
                Pose pose = poses[index];
                if (!IsFinite(pose.position) || !IsFinite(pose.rotation))
                    throw new InvalidOperationException(
                        $"Customer flow {owner} pose {index} must be finite.");
            }
        }

        private static void RequireContinuous(Pose left, Pose right, string owner)
        {
            float positionGap = Vector3.Distance(left.position, right.position);
            float rotationGap = Quaternion.Angle(left.rotation, right.rotation);
            if (positionGap > PositionTolerance || rotationGap > RotationTolerance)
                throw new InvalidOperationException(
                    $"Customer flow {owner} is discontinuous: position gap " +
                    $"{positionGap}, rotation gap {rotationGap}.");
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            IsFinite(value.x) && IsFinite(value.y) &&
            IsFinite(value.z) && IsFinite(value.w);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ValidateStore(GameEntity store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (!store.isStore || !store.hasEntityId)
                throw new InvalidOperationException(
                    "Customer-flow infrastructure requires a configured store.");
        }
    }
}
