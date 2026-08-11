using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class GameEventFactory : IGameEventFactory
    {
        public void EmitAudio(AudioCueId cue) => CreateEntity.Empty()
            .AddAudioCue(cue);

        public void EmitNotification(LocalizedText message) => CreateEntity.Empty()
            .AddNotificationMessage(message);
    }
}
