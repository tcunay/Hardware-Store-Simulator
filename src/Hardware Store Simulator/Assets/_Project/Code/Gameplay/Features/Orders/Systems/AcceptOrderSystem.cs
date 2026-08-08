using System;
using Entitas;
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
                GameEntity target = _gameContext.GetEntityWithEntityId(request.TargetEntityId);
                if (!target.isOrderCounter)
                    continue;

                GameEntity player = _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                if (!player.isPlayer)
                    throw new InvalidOperationException($"Interaction source {request.SourceEntityId} is not a player.");
                if (target.OrderEntityId != player.OrderEntityId)
                    continue;

                GameEntity order = _gameContext.GetEntityWithEntityId(player.OrderEntityId);
                if (!order.isOrder)
                    throw new InvalidOperationException($"Entity {player.OrderEntityId} is not an order.");
                if (!order.isOrderWaiting)
                    continue;

                order.isOrderWaiting = false;
                order.isOrderActive = true;
                _events.EmitNotification($"Заказ принят: {order.RequiredProductCount} мешков цемента");
                _events.EmitAudio(AudioCueId.OrderAccepted);
            }
        }
    }
}
