using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Factories
{
    public interface IGameEventFactory
    {
        void EmitAudio(AudioCueId cue);
        void EmitNotification(string message);
        void EmitProductLoaded(int productEntityId, int orderEntityId);
        void EmitProductStocked(int productEntityId, int deliveryEntityId);
        void EmitOrderCompleted(int orderEntityId);
    }
}
