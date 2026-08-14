using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "CustomerFlowConfig",
        menuName = "Hardware Store/Gameplay/Customer Flow Config")]
    public sealed class CustomerFlowConfig : ScriptableObject, IValidatableConfig
    {
        [SerializeField, Min(1)] private int _parkingCapacity = 3;
        [SerializeField, Min(0f)] private float _firstArrivalDelay = 10f;
        [SerializeField] private CustomerArrivalSchedulePoint[] _arrivalSchedule =
        {
            new(8 * 60, 45f),
            new(10 * 60, 36f),
            new(13 * 60, 26f),
            new(17 * 60, 28f),
            new(19 * 60, 45f),
            new(20 * 60, 70f)
        };

        [Header("Patience")]
        [SerializeField, Min(0.01f)] private float _defaultPatienceDuration = 120f;
        [SerializeField, Min(0.01f)] private float _patienceWarningThreshold = 30f;

        public int ParkingCapacity => _parkingCapacity;
        public float FirstArrivalDelay => _firstArrivalDelay;
        public IReadOnlyList<CustomerArrivalSchedulePoint> ArrivalSchedule => _arrivalSchedule;
        public float DefaultPatienceDuration => _defaultPatienceDuration;
        public float PatienceWarningThreshold => _patienceWarningThreshold;

        public void Configure(int parkingCapacity, float firstArrivalDelay,
            CustomerArrivalSchedulePoint[] arrivalSchedule)
        {
            Configure(
                parkingCapacity,
                firstArrivalDelay,
                arrivalSchedule,
                120f,
                30f);
        }

        public void Configure(int parkingCapacity, float firstArrivalDelay,
            CustomerArrivalSchedulePoint[] arrivalSchedule,
            float defaultPatienceDuration, float patienceWarningThreshold)
        {
            _parkingCapacity = parkingCapacity;
            _firstArrivalDelay = firstArrivalDelay;
            _arrivalSchedule = arrivalSchedule == null
                ? null
                : (CustomerArrivalSchedulePoint[])arrivalSchedule.Clone();
            _defaultPatienceDuration = defaultPatienceDuration;
            _patienceWarningThreshold = patienceWarningThreshold;
            Validate();
        }

        public void Validate()
        {
            const string owner = nameof(CustomerFlowConfig);
            ConfigValidation.RequirePositive(_parkingCapacity, owner, nameof(ParkingCapacity));
            ConfigValidation.RequireNonNegative(
                _firstArrivalDelay,
                owner,
                nameof(FirstArrivalDelay));
            ConfigValidation.RequirePositive(
                _defaultPatienceDuration,
                owner,
                nameof(DefaultPatienceDuration));
            ConfigValidation.RequirePositive(
                _patienceWarningThreshold,
                owner,
                nameof(PatienceWarningThreshold));
            if (_patienceWarningThreshold >= _defaultPatienceDuration)
            {
                throw new InvalidOperationException(
                    $"{owner}.{nameof(PatienceWarningThreshold)} must be lower than " +
                    $"{nameof(DefaultPatienceDuration)}.");
            }
            if (_arrivalSchedule == null || _arrivalSchedule.Length < 2)
                throw new InvalidOperationException(
                    $"{owner}.{nameof(ArrivalSchedule)} must contain at least two points.");

            int previousMinute = -1;
            for (int index = 0; index < _arrivalSchedule.Length; index++)
            {
                CustomerArrivalSchedulePoint point = _arrivalSchedule[index] ??
                    throw new InvalidOperationException(
                        $"{owner}.{nameof(ArrivalSchedule)}[{index}] must be configured.");
                point.Validate(owner, index);
                if (point.Minute <= previousMinute)
                {
                    throw new InvalidOperationException(
                        $"{owner}.{nameof(ArrivalSchedule)} minutes must be strictly increasing.");
                }

                previousMinute = point.Minute;
            }
        }
    }

    // Minute and delay form one indivisible interpolation point on the arrival curve.
    [Serializable]
    public sealed class CustomerArrivalSchedulePoint
    {
        [SerializeField, Range(0, 1439)] private int _minute;
        [SerializeField, Min(0.01f)] private float _delay;

        public CustomerArrivalSchedulePoint(int minute, float delay)
        {
            _minute = minute;
            _delay = delay;
        }

        public int Minute => _minute;
        public float Delay => _delay;

        internal void Validate(string owner, int index)
        {
            string pointOwner = $"{owner}.{nameof(CustomerFlowConfig.ArrivalSchedule)}[{index}]";
            if (_minute < 0 || _minute >= 24 * 60)
            {
                throw new InvalidOperationException(
                    $"{pointOwner}.{nameof(Minute)} must be in range [0, 1439].");
            }
            ConfigValidation.RequirePositive(_delay, pointOwner, nameof(Delay));
        }
    }
}
