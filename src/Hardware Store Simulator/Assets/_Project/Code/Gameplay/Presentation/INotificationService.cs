using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Presentation
{
    public interface INotificationService
    {
        void Show(LocalizedText message);
    }
}
