using System.Globalization;

namespace HardwareStore.Gameplay.Localization
{
    public interface ILocalizationService
    {
        LanguageId Language { get; }
        CultureInfo Culture { get; }

        void Load(LanguageId language);
        string Resolve(LocalizationKey key);
        string Resolve(LocalizedText text);
    }
}
