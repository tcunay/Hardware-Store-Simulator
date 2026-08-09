using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class GameEventFactory : IGameEventFactory
    {
        public void EmitAudio(AudioCueId cue) => CreateEntity.Empty()
            .AddAudioCue(cue);

        public void EmitNotification(string message) => CreateEntity.Empty()
            .AddNotificationMessage(message);

        public void EmitProductLoaded(int productEntityId, int orderEntityId) => CreateEntity.Empty()
            .AddProductEntityId(productEntityId)
            .AddOrderEntityId(orderEntityId)
            .With(x => x.isProductLoaded = true);

        public void EmitProductStocked(int productEntityId, int deliveryEntityId) => CreateEntity.Empty()
            .AddProductEntityId(productEntityId)
            .AddDeliveryEntityId(deliveryEntityId)
            .With(x => x.isProductStocked = true);

        public void EmitOrderCompleted(int orderEntityId) => CreateEntity.Empty()
            .AddOrderEntityId(orderEntityId)
            .With(x => x.isOrderCompletedEvent = true);
    }
}
