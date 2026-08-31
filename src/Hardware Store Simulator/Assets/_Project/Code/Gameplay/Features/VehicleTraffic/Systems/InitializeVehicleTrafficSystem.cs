using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.VehicleTraffic;

namespace HardwareStore.Gameplay.Features.VehicleTraffic.Systems
{
    public sealed class InitializeVehicleTrafficSystem : IExecuteSystem
    {
        private readonly IVehicleTrafficService _traffic;
        private readonly IGroup<GameEntity> _observers;
        private readonly List<GameEntity> _buffer = new(2);
        private bool _initialized;

        public InitializeVehicleTrafficSystem(GameContext gameContext,
            IVehicleTrafficService traffic)
        {
            _traffic = traffic;
            _observers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Player,
                    GameMatcher.Transform,
                    GameMatcher.Camera)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            if (_initialized)
                return;

            List<GameEntity> observers = _observers.GetEntities(_buffer);
            if (observers.Count == 0)
                return;
            if (observers.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Vehicle traffic requires exactly one player observer, found " +
                    $"{observers.Count}.");
            }

            _traffic.Initialize(observers[0].Camera.transform);
            _initialized = true;
        }
    }
}
