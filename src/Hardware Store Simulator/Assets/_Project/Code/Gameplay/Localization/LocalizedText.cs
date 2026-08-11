using System;
using System.Collections.Generic;

namespace HardwareStore.Gameplay.Localization
{
    public sealed class LocalizedText
    {
        private readonly LocalizationArgument[] _arguments;

        public LocalizedText(LocalizationKey key, params LocalizationArgument[] arguments)
        {
            if (key == LocalizationKey.None)
                throw new ArgumentOutOfRangeException(nameof(key));

            Key = key;
            _arguments = arguments == null
                ? throw new ArgumentNullException(nameof(arguments))
                : (LocalizationArgument[])arguments.Clone();
            Arguments = Array.AsReadOnly(_arguments);
        }

        public LocalizationKey Key { get; }
        public IReadOnlyList<LocalizationArgument> Arguments { get; }

        internal LocalizationArgument[] CopyArguments() =>
            (LocalizationArgument[])_arguments.Clone();
    }
}
