using System.Collections.Generic;
using System.Globalization;

namespace HardwareStore.Gameplay.Localization
{
    public interface ILocalizationCatalog
    {
        LanguageId Language { get; }
        CultureInfo Culture { get; }
        IReadOnlyList<LocalizationEntry> Entries { get; }
    }
}
