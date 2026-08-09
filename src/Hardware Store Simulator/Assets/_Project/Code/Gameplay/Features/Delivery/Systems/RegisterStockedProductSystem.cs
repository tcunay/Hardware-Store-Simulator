using System;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Delivery.Systems
{
    public sealed class RegisterStockedProductSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _stockedEvents;

        public RegisterStockedProductSystem(GameContext gameContext, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _stockedEvents = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.ProductStocked,
                GameMatcher.ProductEntityId,
                GameMatcher.DeliveryEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity stockedEvent in _stockedEvents)
            {
                GameEntity delivery =
                    _gameContext.GetEntityWithEntityId(stockedEvent.DeliveryEntityId);
                if (!delivery.isDelivery || !delivery.isDeliveryActive)
                    throw new InvalidOperationException(
                        "A product can only be stocked for an active delivery.");

                GameEntity product =
                    _gameContext.GetEntityWithEntityId(stockedEvent.ProductEntityId);
                if (!product.isProduct || !product.isInStock || product.isInboundProduct ||
                    product.ProductType != delivery.ProductType)
                    throw new InvalidOperationException(
                        "The stocked product does not satisfy its delivery.");

                int stocked = delivery.StockedProductCount;
                if (stocked >= delivery.DeliveryProductCount)
                    throw new InvalidOperationException(
                        $"Delivery {delivery.EntityId} already contains all registered products.");

                stocked++;
                delivery.ReplaceStockedProductCount(stocked);
                _events.EmitNotification(
                    $"Принято на склад: {stocked}/{delivery.DeliveryProductCount}");
                _events.EmitAudio(AudioCueId.ProductStocked);
            }
        }
    }
}
