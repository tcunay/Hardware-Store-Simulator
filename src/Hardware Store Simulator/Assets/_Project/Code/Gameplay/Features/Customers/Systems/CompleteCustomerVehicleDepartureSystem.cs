using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class CompleteCustomerVehicleDepartureSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly CustomerVehicleConfig _config;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public CompleteCustomerVehicleDepartureSystem(GameContext gameContext,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _config = staticData.CustomerVehicle;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.CustomerVisitDeparting,
                    GameMatcher.Order,
                    GameMatcher.OrderRewarded,
                    GameMatcher.OrderContentReleased,
                    GameMatcher.EntityId,
                    GameMatcher.CustomerVisitStoreEntityId,
                    GameMatcher.LoadingZone,
                    GameMatcher.RouteCompleted)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
            {
                if (_gameContext.GetEntitiesWithOrderEntityId(visit.EntityId).Count != 0)
                {
                    throw new InvalidOperationException(
                        $"Departed customer visit {visit.EntityId} still owns order content.");
                }

                GameEntity store = _gameContext.GetEntityWithEntityId(
                    visit.CustomerVisitStoreEntityId);
                if (store.hasCustomerCooldownRemaining)
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} references an invalid active store.");

                visit.RemoveCustomerVisitStoreEntityId();
                visit.isDestructed = true;
                store.AddCustomerCooldownRemaining(_config.NextCustomerDelay);
            }
        }
    }
}
