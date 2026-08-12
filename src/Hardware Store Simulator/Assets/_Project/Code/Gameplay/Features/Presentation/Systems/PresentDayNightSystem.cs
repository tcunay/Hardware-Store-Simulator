using System;
using Entitas;
using HardwareStore.Gameplay.Presentation;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Presentation.Systems
{
    public sealed class PresentDayNightSystem : IExecuteSystem
    {
        private readonly IDayNightPresentationService _presentation;
        private readonly int _startMinute;
        private readonly int _closingMinute;
        private readonly IGroup<GameEntity> _stores;

        public PresentDayNightSystem(GameContext gameContext,
            IStaticDataService staticData,
            IDayNightPresentationService presentation)
        {
            _presentation = presentation;
            _startMinute = staticData.StoreDay.StartMinute;
            _closingMinute = staticData.StoreDay.ClosingMinute;
            _stores = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Store,
                    GameMatcher.EntityId,
                    GameMatcher.CurrentDayMinute)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity store in _stores)
            {
                float currentMinute = store.CurrentDayMinute;
                if (float.IsNaN(currentMinute) || float.IsInfinity(currentMinute) ||
                    currentMinute < _startMinute || currentMinute > _closingMinute)
                {
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} has day minute {currentMinute} outside " +
                        $"the configured range {_startMinute}-{_closingMinute}.");
                }

                float normalizedTime =
                    (currentMinute - _startMinute) /
                    (_closingMinute - _startMinute);
                _presentation.Present(new DayNightSnapshot(normalizedTime));
            }
        }
    }
}
