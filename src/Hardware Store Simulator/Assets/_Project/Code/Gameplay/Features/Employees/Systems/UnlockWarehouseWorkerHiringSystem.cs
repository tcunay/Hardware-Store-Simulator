using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class UnlockWarehouseWorkerHiringSystem : IExecuteSystem
    {
        private readonly WarehouseWorkerConfig _config;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _stores;
        private readonly List<GameEntity> _buffer = new(4);

        public UnlockWarehouseWorkerHiringSystem(GameContext gameContext,
            IStaticDataService staticData, IGameEventFactory events)
        {
            _config = staticData.WarehouseWorker;
            _events = events;
            _stores = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Store,
                    GameMatcher.EntityId,
                    GameMatcher.CompletedOrderCount,
                    GameMatcher.StoreControlTerminalEntityId)
                .NoneOf(
                    GameMatcher.WarehouseWorkerHiringUnlocked,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity store in _stores.GetEntities(_buffer))
            {
                if (store.CompletedOrderCount < 0)
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} has a negative completed-order count.");
                if (store.CompletedOrderCount < _config.RequiredCompletedOrderCount)
                    continue;

                store.isWarehouseWorkerHiringUnlocked = true;
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationWarehouseWorkerUnlocked,
                    _config.HirePrice,
                    _config.DailyWage));
            }
        }
    }
}
