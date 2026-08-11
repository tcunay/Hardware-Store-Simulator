using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Trolley.Systems
{
    public sealed class DetachPushedTrolleySystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;
        private readonly List<GameEntity> _buffer = new(4);

        public DetachPushedTrolleySystem(GameContext gameContext,
            InputContext inputContext, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Player,
                    GameMatcher.EntityId,
                    GameMatcher.HandsOccupied,
                    GameMatcher.PushingTrolley)
                .NoneOf(GameMatcher.ModalOpen));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(
                InputMatcher.InputState,
                InputMatcher.TrolleyPressed));
        }

        public void Execute()
        {
            foreach (InputEntity ignored in _inputs)
            foreach (GameEntity player in _players.GetEntities(_buffer))
            {
                GameEntity trolley =
                    _gameContext.GetEntityWithTrolleyPusherEntityId(player.EntityId);
                if (trolley == null || !trolley.isPlatformTrolley ||
                    trolley.isDestructed || player.isCarryingProduct)
                {
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} has invalid pushed-trolley state.");
                }

                trolley.RemoveTrolleyPusherEntityId();
                trolley.isInteractable = true;
                player.isPushingTrolley = false;
                player.isHandsOccupied = false;
                _events.EmitAudio(AudioCueId.Drop);
            }
        }
    }
}
