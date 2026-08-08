using System;

namespace HardwareStore.Common.Extensions
{
    public static class FunctionalExtensions
    {
        public static T With<T>(this T value, Action<T> apply)
        {
            apply(value);
            return value;
        }

        public static T With<T>(this T value, Action<T> apply, bool when)
        {
            if (when)
                apply(value);

            return value;
        }
    }
}
