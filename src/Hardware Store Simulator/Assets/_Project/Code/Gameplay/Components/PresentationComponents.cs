using Entitas;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class NotificationMessage : IComponent { public LocalizedText Value; }
    [Game] public class AudioCue : IComponent { public AudioCueId Value; }
}
