using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Infrastructure.Identifiers;
using HardwareStore.Infrastructure.View;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class InteractionTargetFactory : IInteractionTargetFactory
    {
        private readonly IIdentifierService _identifiers;

        public InteractionTargetFactory(IIdentifierService identifiers) => _identifiers = identifiers;

        public GameEntity CreateOrderCounter(EntityBehaviour view, int orderEntityId)
        {
            int entityId = _identifiers.Next();
            GameEntity entity = CreateEntity.Empty(entityId)
                .AddOrderEntityId(orderEntityId)
                .With(x => x.isOrderCounter = true)
                .With(x => x.isInteractable = true);

            view.SetEntity(entity);
            return entity;
        }

        public GameEntity CreateLoadingZone(EntityBehaviour view, int orderEntityId)
        {
            int entityId = _identifiers.Next();
            GameEntity entity = CreateEntity.Empty(entityId)
                .AddOrderEntityId(orderEntityId)
                .With(x => x.isLoadingZone = true)
                .With(x => x.isInteractable = true);

            view.SetEntity(entity);
            return entity;
        }
    }
}
