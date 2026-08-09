using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Delivery.Systems
{
    public sealed class CompleteDeliverySystem : IExecuteSystem
    {
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _deliveries;
        private readonly List<GameEntity> _buffer = new(4);

        public CompleteDeliverySystem(GameContext gameContext, IGameEventFactory events)
        {
            _events = events;
            _deliveries = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Delivery,
                GameMatcher.DeliveryActive,
                GameMatcher.EntityId,
                GameMatcher.DeliveryProcurementTerminalEntityId,
                GameMatcher.DeliveryProductCount,
                GameMatcher.StockedProductCount)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity delivery in _deliveries.GetEntities(_buffer))
            {
                if (delivery.StockedProductCount < delivery.DeliveryProductCount)
                    continue;
                if (delivery.StockedProductCount > delivery.DeliveryProductCount)
                    throw new InvalidOperationException(
                        $"Delivery {delivery.EntityId} registered more products than expected.");

                delivery.RemoveDeliveryProcurementTerminalEntityId();
                delivery.isDeliveryActive = false;
                delivery.isDeliveryCompleted = true;
                _events.EmitNotification("Поставка полностью принята на склад");
                _events.EmitAudio(AudioCueId.DeliveryCompleted);
                delivery.isDestructed = true;
            }
        }
    }
}
