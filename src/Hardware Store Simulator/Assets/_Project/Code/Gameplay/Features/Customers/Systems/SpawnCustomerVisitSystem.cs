using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Scene;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class SpawnCustomerVisitSystem : IExecuteSystem
    {
        private readonly IStoreSceneData _sceneData;
        private readonly ICustomerVisitFactory _customerVisitFactory;
        private readonly IGroup<GameEntity> _stores;
        private readonly List<GameEntity> _buffer = new(4);

        public SpawnCustomerVisitSystem(GameContext gameContext, IStoreSceneData sceneData,
            ICustomerVisitFactory customerVisitFactory)
        {
            _sceneData = sceneData;
            _customerVisitFactory = customerVisitFactory;
            _stores = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Store,
                    GameMatcher.EntityId,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.CustomerCooldownRemaining)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity store in _stores.GetEntities(_buffer))
            {
                if (store.CustomerCooldownRemaining > 0f)
                    continue;

                Pose[] arrivalRoute = _sceneData.GetRoute(SceneRouteId.CustomerVehicleArrival);
                Pose[] departureRoute = _sceneData.GetRoute(SceneRouteId.CustomerVehicleDeparture);
                _customerVisitFactory.Create(store, arrivalRoute, departureRoute);
            }
        }
    }
}
