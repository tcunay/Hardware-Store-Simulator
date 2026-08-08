using Entitas;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class NotificationMessage : IComponent { public string Value; }
    [Game] public class AudioCue : IComponent { public AudioCueId Value; }
}
