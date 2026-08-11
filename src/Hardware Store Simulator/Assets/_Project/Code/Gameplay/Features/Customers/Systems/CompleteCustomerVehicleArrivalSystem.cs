using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Scene;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class CompleteCustomerVehicleArrivalSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly ICustomerFactory _customerFactory;
        private readonly IStoreSceneData _sceneData;
        private readonly CustomerVehicleConfig _vehicleConfig;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public CompleteCustomerVehicleArrivalSystem(GameContext gameContext,
            ICustomerFactory customerFactory, IStoreSceneData sceneData,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _customerFactory = customerFactory;
            _sceneData = sceneData;
            _vehicleConfig = staticData.CustomerVehicle;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.CustomerVisitArriving,
                    GameMatcher.EntityId,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.CustomerProjectType,
                    GameMatcher.CustomerProjectTitle,
                    GameMatcher.CustomerRequest,
                    GameMatcher.LoadingZone,
                    GameMatcher.Slots,
                    GameMatcher.Route,
                    GameMatcher.RouteWaypointIndex,
                    GameMatcher.RouteCompleted)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
            {
                ValidateCargoSlots(visit);
                if (_gameContext.GetEntityWithCustomerActorVisitEntityId(visit.EntityId) != null)
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} already has a customer actor.");

                Pose[] routeToCounter =
                    _sceneData.GetRoute(SceneRouteId.CustomerWalkToCounter);
                Pose[] routeToVehicle =
                    _sceneData.GetRoute(SceneRouteId.CustomerWalkToVehicle);
                _customerFactory.Create(visit, routeToCounter, routeToVehicle);

                visit.isRouteCompleted = false;
                visit.RemoveRoute();
                visit.RemoveRouteWaypointIndex();
            }
        }

        private void ValidateCargoSlots(GameEntity visit)
        {
            if (visit.Slots.Length != _vehicleConfig.CargoCapacity)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} exposes {visit.Slots.Length} loading " +
                    $"slots, but {nameof(CustomerVehicleConfig)} requires " +
                    $"{_vehicleConfig.CargoCapacity}.");
            }
        }
    }
}
