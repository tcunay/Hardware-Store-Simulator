using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Presentation
{
    public interface IAudioService
    {
        void Play(AudioCueId cue);
    }
}
