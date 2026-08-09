using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HardwareStore.Gameplay.Configs
{
    internal static class ConfigValidation
    {
        public static void RequireReference(Object value, string owner, string property)
        {
            if (value == null)
                throw Invalid(owner, property, "must reference an object");
        }

        public static void RequirePositive(float value, string owner, string property)
        {
            RequireFinite(value, owner, property);
            if (value <= 0f)
                throw Invalid(owner, property, "must be greater than zero");
        }

        public static void RequireNonNegative(float value, string owner, string property)
        {
            RequireFinite(value, owner, property);
            if (value < 0f)
                throw Invalid(owner, property, "must not be negative");
        }

        public static void RequireNegative(float value, string owner, string property)
        {
            RequireFinite(value, owner, property);
            if (value >= 0f)
                throw Invalid(owner, property, "must be less than zero");
        }

        public static void RequireInRange(float value, float minimum, float maximum,
            string owner, string property)
        {
            RequireFinite(value, owner, property);
            if (value < minimum || value > maximum)
                throw Invalid(owner, property, $"must be in range [{minimum}, {maximum}]");
        }

        public static void RequirePositive(int value, string owner, string property)
        {
            if (value <= 0)
                throw Invalid(owner, property, "must be greater than zero");
        }

        public static void RequireNonNegative(int value, string owner, string property)
        {
            if (value < 0)
                throw Invalid(owner, property, "must not be negative");
        }

        public static void RequireFinite(Vector3 value, string owner, string property)
        {
            RequireFinite(value.x, owner, property);
            RequireFinite(value.y, owner, property);
            RequireFinite(value.z, owner, property);
        }

        public static void RequireDefined<TEnum>(TEnum value, string owner, string property)
            where TEnum : struct, Enum
        {
            if (!Enum.IsDefined(typeof(TEnum), value))
                throw Invalid(owner, property, $"contains undefined {typeof(TEnum).Name} value {value}");
        }

        public static void RequireProductFitsInt(int left, int right, string owner, string property)
        {
            long product = (long)left * right;
            if (product > int.MaxValue || product < int.MinValue)
                throw Invalid(owner, property, "must fit a 32-bit signed integer");
        }

        private static void RequireFinite(float value, string owner, string property)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw Invalid(owner, property, "must be finite");
        }

        private static InvalidOperationException Invalid(string owner, string property,
            string requirement) =>
            new($"{owner}.{property} {requirement}.");
    }
}
