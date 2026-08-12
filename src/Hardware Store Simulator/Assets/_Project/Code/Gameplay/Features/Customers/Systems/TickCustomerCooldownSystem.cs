using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Time;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class TickCustomerCooldownSystem : IExecuteSystem
    {
        private readonly ITimeService _time;
        private readonly IGroup<GameEntity> _stores;
        private readonly List<GameEntity> _buffer = new(4);

        public TickCustomerCooldownSystem(GameContext gameContext, ITimeService time)
        {
            _time = time;
            _stores = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Store,
                    GameMatcher.StoreOpen,
                    GameMatcher.EntityId,
                    GameMatcher.CustomerCooldownRemaining)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity store in _stores.GetEntities(_buffer))
            {
                float remaining = Math.Max(0f, store.CustomerCooldownRemaining - _time.DeltaTime);
                store.ReplaceCustomerCooldownRemaining(remaining);
            }
        }
    }
}
