using System;
using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Orders.Systems
{
    public sealed class AcceptOrderSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public AcceptOrderSystem(GameContext gameContext, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _requests = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.InteractionRequest,
                GameMatcher.SourceEntityId,
                GameMatcher.TargetEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity request in _requests)
            {
                GameEntity orderCounter = _gameContext.GetRequiredEntity(
                    request.TargetEntityId,
                    "interaction target");
                if (!orderCounter.isOrderCounter)
                    continue;

                GameEntity player = _gameContext.GetRequiredEntity(
                    request.SourceEntityId,
                    "interaction source");
                if (!player.isPlayer)
                    throw new InvalidOperationException(
                        $"Interaction source {request.SourceEntityId} is not a player.");
                if (!player.hasStoreEntityId)
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} has no store relation.");

                GameEntity order = _gameContext.GetRequiredEntity(
                    orderCounter.OrderEntityId,
                    "order counter order");
                if (!order.isOrder ||
                    !order.hasStoreEntityId ||
                    !order.hasStorageZoneEntityId)
                    throw new InvalidOperationException(
                        $"Order counter {orderCounter.EntityId} has an invalid order relation.");
                if (order.StoreEntityId != player.StoreEntityId)
                    continue;
                if (!order.isOrderWaiting)
                    continue;

                if (order.AvailableProductCount < order.RequiredProductCount)
                {
                    _events.EmitNotification(
                        $"Недостаточно товара на складе: " +
                        $"{order.AvailableProductCount}/{order.RequiredProductCount}");
                    continue;
                }

                order.isOrderWaiting = false;
                order.isOrderActive = true;
                _events.EmitNotification(
                    $"Заказ принят: {order.RequiredProductCount} мешков цемента");
                _events.EmitAudio(AudioCueId.OrderAccepted);
            }
        }
    }
}
