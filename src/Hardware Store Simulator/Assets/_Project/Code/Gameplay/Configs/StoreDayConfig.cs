using System;
using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "StoreDayConfig",
        menuName = "Hardware Store/Gameplay/Store Day Config")]
    public sealed class StoreDayConfig : ScriptableObject, IValidatableConfig
    {
        [SerializeField, Range(0, 1439)] private int _startMinute = 480;
        [SerializeField, Range(1, 1439)] private int _closingMinute = 1200;
        [SerializeField, Min(0.01f)] private float _dayDurationSeconds = 480f;

        public int StartMinute => _startMinute;
        public int ClosingMinute => _closingMinute;
        public float DayDurationSeconds => _dayDurationSeconds;

        public void Validate()
        {
            const string owner = nameof(StoreDayConfig);
            if (_startMinute < 0 || _startMinute >= 1440)
                throw new InvalidOperationException(
                    $"{owner}.{nameof(StartMinute)} must be in range [0, 1439].");
            if (_closingMinute <= _startMinute || _closingMinute > 1439)
                throw new InvalidOperationException(
                    $"{owner}.{nameof(ClosingMinute)} must be greater than " +
                    $"{nameof(StartMinute)} and no greater than 1439.");
            ConfigValidation.RequirePositive(
                _dayDurationSeconds,
                owner,
                nameof(DayDurationSeconds));
        }
    }
}
