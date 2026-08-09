using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class GameEventFactory : IGameEventFactory
    {
        public void EmitAudio(AudioCueId cue) => CreateEntity.Empty()
            .AddAudioCue(cue);

        public void EmitNotification(string message) => CreateEntity.Empty()
            .AddNotificationMessage(message);
    }
}
