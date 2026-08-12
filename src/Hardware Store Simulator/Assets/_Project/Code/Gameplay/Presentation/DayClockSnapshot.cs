using System;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct DayClockSnapshot
    {
        public DayClockSnapshot(int dayNumber, int currentDayMinute, StoreDayPhase phase)
        {
            if (dayNumber <= 0)
                throw new ArgumentOutOfRangeException(nameof(dayNumber));
            if (currentDayMinute < 0 || currentDayMinute >= 24 * 60)
                throw new ArgumentOutOfRangeException(nameof(currentDayMinute));
            if (!Enum.IsDefined(typeof(StoreDayPhase), phase))
                throw new ArgumentOutOfRangeException(nameof(phase));

            DayNumber = dayNumber;
            CurrentDayMinute = currentDayMinute;
            Phase = phase;
        }

        public int DayNumber { get; }
        public int CurrentDayMinute { get; }
        public StoreDayPhase Phase { get; }
    }
}
