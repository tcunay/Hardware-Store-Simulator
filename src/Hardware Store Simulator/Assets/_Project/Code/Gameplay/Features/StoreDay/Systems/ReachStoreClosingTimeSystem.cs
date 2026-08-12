using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.StoreDay.Systems
{
    public sealed class ReachStoreClosingTimeSystem : IExecuteSystem
    {
        private readonly int _closingMinute;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _stores;
        private readonly List<GameEntity> _buffer = new(1);

        public ReachStoreClosingTimeSystem(GameContext gameContext,
            IStaticDataService staticData, IGameEventFactory events)
        {
            _closingMinute = staticData.StoreDay.ClosingMinute;
            _events = events;
            _stores = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Store,
                    GameMatcher.StoreOpen,
                    GameMatcher.CurrentDayMinute)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity store in _stores.GetEntities(_buffer))
            {
                if (store.CurrentDayMinute < _closingMinute)
                    continue;

                store.ReplaceCurrentDayMinute(_closingMinute);
                store.isStoreOpen = false;
                store.isStoreClosing = true;
                if (store.hasCustomerCooldownRemaining)
                    store.RemoveCustomerCooldownRemaining();
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationStoreClosingTime));
            }
        }
    }
}
