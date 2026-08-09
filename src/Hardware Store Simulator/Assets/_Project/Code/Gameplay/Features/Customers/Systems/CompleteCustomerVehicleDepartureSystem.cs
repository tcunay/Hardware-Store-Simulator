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
                    GameMatcher.EntityId,
                    GameMatcher.CustomerVisitStoreEntityId,
                    GameMatcher.LoadingZone,
                    GameMatcher.LoadedProductCount,
                    GameMatcher.RequiredProductCount,
                    GameMatcher.RouteCompleted)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
                CompleteVisit(visit);
        }

        private void CompleteVisit(GameEntity visit)
        {
            GameEntity store = _gameContext.GetEntityWithEntityId(
                visit.CustomerVisitStoreEntityId);
            if (store.hasCustomerCooldownRemaining)
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} references an invalid active store.");

            int loadedProductCount = 0;
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithCustomerVisitEntityId(visit.EntityId))
            {
                if (!product.isProduct || !product.isLoaded || product.isDestructed)
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has invalid linked entity " +
                        $"{product.EntityId}.");

                product.isDestructed = true;
                loadedProductCount++;
            }

            if (loadedProductCount != visit.LoadedProductCount ||
                loadedProductCount != visit.RequiredProductCount)
            {
                throw new InvalidOperationException(
                    $"Departed customer visit {visit.EntityId} contains {loadedProductCount} " +
                    $"products, but its order contains {visit.LoadedProductCount}/" +
                    $"{visit.RequiredProductCount}.");
            }

            visit.RemoveCustomerVisitStoreEntityId();
            visit.isDestructed = true;
            store.AddCustomerCooldownRemaining(_config.NextCustomerDelay);
        }
    }
}
