using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
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

        public void EmitCustomerPatienceWarning(int customerVisitEntityId) =>
            CreateEntity.Empty()
                .AddCustomerEventVisitEntityId(customerVisitEntityId)
                .With(x => x.isCustomerPatienceWarningEvent = true);

        public void EmitCustomerAbandoned(int customerVisitEntityId) =>
            CreateEntity.Empty()
                .AddCustomerEventVisitEntityId(customerVisitEntityId)
                .With(x => x.isCustomerAbandonedEvent = true);
    }
}
