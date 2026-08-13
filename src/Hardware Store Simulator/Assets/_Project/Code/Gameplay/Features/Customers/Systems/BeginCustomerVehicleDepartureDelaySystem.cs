using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class BeginCustomerVehicleDepartureDelaySystem : IExecuteSystem
    {
        private readonly CustomerVehicleConfig _config;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public BeginCustomerVehicleDepartureDelaySystem(GameContext gameContext,
            IStaticDataService staticData)
        {
            _config = staticData.CustomerVehicle;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.Order,
                    GameMatcher.CustomerVisitCompleted,
                    GameMatcher.OrderRewarded,
                    GameMatcher.EntityId,
                    GameMatcher.ReservedCustomerLoadingBayEntityId)
                .NoneOf(
                    GameMatcher.CustomerDepartureDelayRemaining,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
            {
                visit.isInteractable = false;
                visit.AddCustomerDepartureDelayRemaining(_config.CompletedDwellDuration);
            }
        }
    }
}
