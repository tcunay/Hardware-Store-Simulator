using System;

namespace HardwareStore.Gameplay.Localization
{
    public readonly struct LocalizationArgument
    {
        private readonly long _number;
        private readonly LocalizedText _text;

        private LocalizationArgument(long number)
        {
            _number = number;
            _text = null;
            IsText = false;
        }

        private LocalizationArgument(LocalizedText text)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _number = default;
            IsText = true;
        }

        public bool IsText { get; }
        public long Number => !IsText
            ? _number
            : throw new InvalidOperationException("A text localization argument has no number.");
        public LocalizedText Text => IsText
            ? _text
            : throw new InvalidOperationException("A numeric localization argument has no text.");

        public static implicit operator LocalizationArgument(int value) => new(value);
        public static implicit operator LocalizationArgument(long value) => new(value);
        public static implicit operator LocalizationArgument(LocalizedText value) => new(value);
    }
}
