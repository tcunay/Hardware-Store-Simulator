using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Factories
{
    public interface IGameEventFactory
    {
        void EmitAudio(AudioCueId cue);
        void EmitNotification(string message);
    }
}
