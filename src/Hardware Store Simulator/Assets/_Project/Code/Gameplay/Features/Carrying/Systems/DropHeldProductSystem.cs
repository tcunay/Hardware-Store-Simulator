using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Carrying.Systems
{
    public sealed class DropHeldProductSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;
        private readonly List<GameEntity> _buffer = new(4);

        public DropHeldProductSystem(GameContext gameContext, InputContext inputContext, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.HeldProductId,
                GameMatcher.DropOrigin));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(
                InputMatcher.InputState,
                InputMatcher.DropPressed));
        }

        public void Execute()
        {
            foreach (InputEntity ignored in _inputs)
            foreach (GameEntity player in _players.GetEntities(_buffer))
            {
                GameEntity product = _gameContext.GetEntityWithEntityId(player.HeldProductId);
                Transform dropOrigin = player.DropOrigin;
                player.RemoveHeldProductId();
                product.isCarried = false;
                product.ProductView.Drop(dropOrigin.position + dropOrigin.forward * 1.15f, dropOrigin.rotation);
                _events.EmitAudio(AudioCueId.Drop);
            }
        }
    }
}
