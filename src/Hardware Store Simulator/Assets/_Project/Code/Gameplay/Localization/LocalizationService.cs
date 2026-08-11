using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HardwareStore.Gameplay.Localization
{
    public sealed class LocalizationService : ILocalizationService
    {
        private readonly ILocalizationCatalog[] _catalogs;
        private CatalogState _state;

        public LocalizationService(IEnumerable<ILocalizationCatalog> catalogs)
        {
            if (catalogs == null)
                throw new ArgumentNullException(nameof(catalogs));

            _catalogs = catalogs.ToArray();
            if (_catalogs.Length == 0)
                throw new InvalidOperationException(
                    "At least one localization catalog must be registered.");
        }

        public LanguageId Language => State.Language;
        public CultureInfo Culture => State.Culture;

        public void Load(LanguageId language)
        {
            if (!Enum.IsDefined(typeof(LanguageId), language))
                throw new ArgumentOutOfRangeException(nameof(language), language,
                    "Localization language must be defined.");

            Dictionary<LanguageId, ILocalizationCatalog> catalogsByLanguage =
                IndexCatalogs();
            if (!catalogsByLanguage.TryGetValue(language, out ILocalizationCatalog catalog))
            {
                throw new InvalidOperationException(
                    $"Localization catalog for language {language} is not registered.");
            }

            CatalogState nextState = BuildState(catalog);
            _state = nextState;
        }

        public string Resolve(LocalizationKey key)
        {
            CatalogState state = State;
            LocalizationEntry entry = GetEntry(state, key);
            if (entry.ArgumentCount != 0)
            {
                throw new InvalidOperationException(
                    $"Localization key {key} requires {entry.ArgumentCount} arguments.");
            }

            return Format(state, entry, Array.Empty<object>());
        }

        public string Resolve(LocalizedText text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            CatalogState state = State;
            return Resolve(state, text);
        }

        private Dictionary<LanguageId, ILocalizationCatalog> IndexCatalogs()
        {
            var catalogsByLanguage =
                new Dictionary<LanguageId, ILocalizationCatalog>(_catalogs.Length);
            for (int index = 0; index < _catalogs.Length; index++)
            {
                ILocalizationCatalog catalog = _catalogs[index] ??
                    throw new InvalidOperationException(
                        $"Localization catalog at index {index} is missing.");
                if (!Enum.IsDefined(typeof(LanguageId), catalog.Language))
                {
                    throw new InvalidOperationException(
                        $"Localization catalog at index {index} uses undefined language " +
                        $"{catalog.Language}.");
                }

                if (!catalogsByLanguage.TryAdd(catalog.Language, catalog))
                {
                    throw new InvalidOperationException(
                        $"Localization language {catalog.Language} is registered more than once.");
                }
            }

            return catalogsByLanguage;
        }

        private static CatalogState BuildState(ILocalizationCatalog catalog)
        {
            CultureInfo sourceCulture = catalog.Culture ??
                throw new InvalidOperationException(
                    $"Localization catalog {catalog.Language} has no culture.");
            IReadOnlyList<LocalizationEntry> sourceEntries = catalog.Entries ??
                throw new InvalidOperationException(
                    $"Localization catalog {catalog.Language} has no entries.");

            var entries = new Dictionary<LocalizationKey, LocalizationEntry>(
                sourceEntries.Count);
            for (int index = 0; index < sourceEntries.Count; index++)
            {
                LocalizationEntry entry = sourceEntries[index];
                ValidateEntry(catalog.Language, entry, index);
                if (!entries.TryAdd(entry.Key, entry))
                {
                    throw new InvalidOperationException(
                        $"Localization catalog {catalog.Language} contains duplicate key " +
                        $"{entry.Key}.");
                }
            }

            LocalizationKey[] expectedKeys = Enum
                .GetValues(typeof(LocalizationKey))
                .Cast<LocalizationKey>()
                .Where(key => key != LocalizationKey.None)
                .ToArray();
            LocalizationKey[] missingKeys = expectedKeys
                .Where(key => !entries.ContainsKey(key))
                .ToArray();
            if (missingKeys.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Localization catalog {catalog.Language} does not cover every " +
                    $"{nameof(LocalizationKey)}. Missing keys: {string.Join(", ", missingKeys)}.");
            }

            if (entries.Count != expectedKeys.Length)
            {
                throw new InvalidOperationException(
                    $"Localization catalog {catalog.Language} must contain exactly " +
                    $"{expectedKeys.Length} entries, but contains {entries.Count}.");
            }

            CultureInfo culture = CultureInfo.ReadOnly(
                (CultureInfo)sourceCulture.Clone());
            return new CatalogState(catalog.Language, culture, entries);
        }

        private static void ValidateEntry(LanguageId language, LocalizationEntry entry,
            int index)
        {
            if (entry.Key == LocalizationKey.None ||
                !Enum.IsDefined(typeof(LocalizationKey), entry.Key))
            {
                throw new InvalidOperationException(
                    $"Localization catalog {language} entry {index} uses invalid key " +
                    $"{entry.Key}.");
            }

            if (string.IsNullOrWhiteSpace(entry.Template))
            {
                throw new InvalidOperationException(
                    $"Localization catalog {language} key {entry.Key} has a blank template.");
            }

            if (entry.ArgumentCount < 0)
            {
                throw new InvalidOperationException(
                    $"Localization catalog {language} key {entry.Key} has negative argument " +
                    "count.");
            }

            ValidateCompositeFormat(language, entry);
        }

        private static void ValidateCompositeFormat(LanguageId language,
            LocalizationEntry entry)
        {
            var usedArguments = new bool[entry.ArgumentCount];
            var probes = new object[entry.ArgumentCount];
            for (int index = 0; index < probes.Length; index++)
                probes[index] = new FormatProbe(index, usedArguments);

            try
            {
                _ = string.Format(
                    CultureInfo.InvariantCulture,
                    entry.Template,
                    probes);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException(
                    $"Localization catalog {language} key {entry.Key} has an invalid " +
                    "composite format template.",
                    exception);
            }

            for (int index = 0; index < usedArguments.Length; index++)
            {
                if (!usedArguments[index])
                {
                    throw new InvalidOperationException(
                        $"Localization catalog {language} key {entry.Key} declares " +
                        $"{entry.ArgumentCount} arguments but does not use argument {index}.");
                }
            }
        }

        private static string Resolve(CatalogState state, LocalizedText text)
        {
            LocalizationEntry entry = GetEntry(state, text.Key);
            if (text.Arguments.Count != entry.ArgumentCount)
            {
                throw new InvalidOperationException(
                    $"Localization key {text.Key} requires {entry.ArgumentCount} arguments, " +
                    $"but received {text.Arguments.Count}.");
            }

            var arguments = new object[text.Arguments.Count];
            for (int index = 0; index < arguments.Length; index++)
            {
                LocalizationArgument argument = text.Arguments[index];
                arguments[index] = argument.IsText
                    ? Resolve(state, argument.Text)
                    : argument.Number;
            }

            return Format(state, entry, arguments);
        }

        private static LocalizationEntry GetEntry(CatalogState state, LocalizationKey key)
        {
            if (key == LocalizationKey.None ||
                !Enum.IsDefined(typeof(LocalizationKey), key))
                throw new ArgumentOutOfRangeException(nameof(key), key,
                    "Localization key must be defined and cannot be None.");

            if (state.Entries.TryGetValue(key, out LocalizationEntry entry))
                return entry;

            throw new InvalidOperationException(
                $"Localization key {key} is missing for language {state.Language}.");
        }

        private static string Format(CatalogState state, LocalizationEntry entry,
            object[] arguments)
        {
            try
            {
                return string.Format(state.Culture, entry.Template, arguments);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException(
                    $"Localization key {entry.Key} could not be formatted for language " +
                    $"{state.Language}.",
                    exception);
            }
        }

        private CatalogState State => _state ??
            throw new InvalidOperationException("Localization data has not been loaded yet.");

        private sealed class CatalogState
        {
            public CatalogState(LanguageId language, CultureInfo culture,
                IReadOnlyDictionary<LocalizationKey, LocalizationEntry> entries)
            {
                Language = language;
                Culture = culture;
                Entries = entries;
            }

            public LanguageId Language { get; }
            public CultureInfo Culture { get; }
            public IReadOnlyDictionary<LocalizationKey, LocalizationEntry> Entries { get; }
        }

        private sealed class FormatProbe : IFormattable
        {
            private readonly int _index;
            private readonly bool[] _usedArguments;

            public FormatProbe(int index, bool[] usedArguments)
            {
                _index = index;
                _usedArguments = usedArguments;
            }

            public string ToString(string format, IFormatProvider formatProvider)
            {
                _usedArguments[_index] = true;
                return "0";
            }

            public override string ToString()
            {
                _usedArguments[_index] = true;
                return "0";
            }
        }
    }
}
