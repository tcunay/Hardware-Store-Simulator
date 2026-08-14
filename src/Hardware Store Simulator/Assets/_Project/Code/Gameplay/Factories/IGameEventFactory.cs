using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Factories
{
    public interface IGameEventFactory
    {
        void EmitAudio(AudioCueId cue);
        void EmitNotification(LocalizedText message);
        void EmitCustomerPatienceWarning(int customerVisitEntityId);
        void EmitCustomerAbandoned(int customerVisitEntityId);
    }
}
