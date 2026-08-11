using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Features.Delivery.Systems
{
    public sealed class RegisterStockedProductSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _stockedProducts;
        private readonly List<GameEntity> _buffer = new(8);

        public RegisterStockedProductSystem(GameContext gameContext, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _stockedProducts = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Product,
                GameMatcher.ProductStocked,
                GameMatcher.DeliveryEntityId,
                GameMatcher.InStock,
                GameMatcher.ProductType));
        }

        public void Execute()
        {
            foreach (GameEntity product in _stockedProducts.GetEntities(_buffer))
            {
                GameEntity delivery =
                    _gameContext.GetEntityWithEntityId(product.DeliveryEntityId);
                if (delivery == null || !delivery.isDeliveryActive)
                    throw new InvalidOperationException(
                        "A product can only be stocked for an active delivery.");

                if (product.isInboundProduct || product.ProductType != delivery.ProductType)
                    throw new InvalidOperationException(
                        "The stocked product does not satisfy its delivery.");

                int stocked = delivery.StockedProductCount;
                if (stocked >= delivery.DeliveryProductCount)
                    throw new InvalidOperationException(
                        $"Delivery {delivery.EntityId} already contains all registered products.");

                stocked++;
                delivery.ReplaceStockedProductCount(stocked);
                product.isProductStocked = false;
                product.RemoveDeliveryEntityId();
                _events.EmitNotification(
                    LocalizedTexts.Text(
                        LocalizationKey.NotificationProductStocked,
                        stocked,
                        delivery.DeliveryProductCount));
                _events.EmitAudio(AudioCueId.ProductStocked);
            }
        }
    }
}
