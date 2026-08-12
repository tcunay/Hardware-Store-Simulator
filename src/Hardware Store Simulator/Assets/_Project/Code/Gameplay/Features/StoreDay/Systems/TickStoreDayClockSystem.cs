using Entitas;
using HardwareStore.Gameplay.Common.Time;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.StoreDay.Systems
{
    public sealed class TickStoreDayClockSystem : IExecuteSystem
    {
        private readonly ITimeService _time;
        private readonly float _minutesPerSecond;
        private readonly IGroup<GameEntity> _stores;

        public TickStoreDayClockSystem(GameContext gameContext,
            IStaticDataService staticData, ITimeService time)
        {
            _time = time;
            StoreDayConfig config = staticData.StoreDay;
            _minutesPerSecond =
                (config.ClosingMinute - config.StartMinute) / config.DayDurationSeconds;
            _stores = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Store,
                    GameMatcher.StoreOpen,
                    GameMatcher.CurrentDayMinute)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity store in _stores)
            {
                store.ReplaceCurrentDayMinute(
                    store.CurrentDayMinute + _time.DeltaTime * _minutesPerSecond);
            }
        }
    }
}
