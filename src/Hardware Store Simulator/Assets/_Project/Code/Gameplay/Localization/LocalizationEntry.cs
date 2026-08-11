using System;

namespace HardwareStore.Gameplay.Localization
{
    public readonly struct LocalizationEntry
    {
        public LocalizationEntry(LocalizationKey key, string template, int argumentCount)
        {
            if (key == LocalizationKey.None)
                throw new ArgumentOutOfRangeException(nameof(key));
            if (argumentCount < 0)
                throw new ArgumentOutOfRangeException(nameof(argumentCount));

            Key = key;
            Template = template ?? throw new ArgumentNullException(nameof(template));
            ArgumentCount = argumentCount;
        }

        public LocalizationKey Key { get; }
        public string Template { get; }
        public int ArgumentCount { get; }
    }
}
