using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Time;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class TickCustomerVehicleDepartureDelaySystem : IExecuteSystem
    {
        private readonly ITimeService _time;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public TickCustomerVehicleDepartureDelaySystem(GameContext gameContext, ITimeService time)
        {
            _time = time;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.Order,
                    GameMatcher.CustomerVisitCompleted,
                    GameMatcher.OrderRewarded,
                    GameMatcher.EntityId,
                    GameMatcher.CustomerDepartureDelayRemaining)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
            {
                float remaining = visit.CustomerDepartureDelayRemaining;
                if (float.IsNaN(remaining) || float.IsInfinity(remaining) || remaining < 0f)
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has invalid departure delay {remaining}.");

                visit.ReplaceCustomerDepartureDelayRemaining(
                    Math.Max(0f, remaining - _time.DeltaTime));
            }
        }
    }
}
