using System;
using System.Collections.Generic;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Common.Customers
{
    public sealed class CustomerArrivalSchedule : ICustomerArrivalSchedule
    {
        private readonly IReadOnlyList<CustomerArrivalSchedulePoint> _points;

        public CustomerArrivalSchedule(IStaticDataService staticData) =>
            _points = staticData.CustomerFlow.ArrivalSchedule;

        public float GetDelay(float currentDayMinute)
        {
            if (float.IsNaN(currentDayMinute) || float.IsInfinity(currentDayMinute))
                throw new ArgumentOutOfRangeException(nameof(currentDayMinute));

            CustomerArrivalSchedulePoint first = _points[0];
            CustomerArrivalSchedulePoint last = _points[_points.Count - 1];
            if (currentDayMinute < first.Minute || currentDayMinute > last.Minute)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentDayMinute),
                    currentDayMinute,
                    $"Customer arrival minute must be in range " +
                    $"[{first.Minute}, {last.Minute}].");
            }

            for (int index = 1; index < _points.Count; index++)
            {
                CustomerArrivalSchedulePoint right = _points[index];
                if (currentDayMinute > right.Minute)
                    continue;

                CustomerArrivalSchedulePoint left = _points[index - 1];
                float progress =
                    (currentDayMinute - left.Minute) / (right.Minute - left.Minute);
                return left.Delay + (right.Delay - left.Delay) * progress;
            }

            return last.Delay;
        }
    }
}
