using System;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct DayNightSnapshot
    {
        public DayNightSnapshot(float normalizedTime)
        {
            if (float.IsNaN(normalizedTime) || float.IsInfinity(normalizedTime) ||
                normalizedTime < 0f || normalizedTime > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(normalizedTime));
            }

            NormalizedTime = normalizedTime;
        }

        public float NormalizedTime { get; }
    }
}
