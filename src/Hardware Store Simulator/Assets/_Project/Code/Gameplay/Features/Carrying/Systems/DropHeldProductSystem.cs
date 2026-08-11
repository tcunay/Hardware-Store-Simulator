using System;
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
                GameMatcher.EntityId,
                GameMatcher.HandsOccupied,
                GameMatcher.DropOrigin)
                .NoneOf(GameMatcher.ModalOpen));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(
                InputMatcher.InputState,
                InputMatcher.DropPressed));
        }

        public void Execute()
        {
            foreach (InputEntity ignored in _inputs)
            foreach (GameEntity player in _players.GetEntities(_buffer))
            {
                GameEntity product = _gameContext.GetEntityWithCarrierEntityId(player.EntityId);
                ValidatePlacementState(product);
                Transform dropOrigin = player.DropOrigin;
                Vector3 position = dropOrigin.position +
                                   dropOrigin.forward * product.DropForwardDistance;

                product.RemoveCarrierEntityId();
                player.isHandsOccupied = false;
                product.isLooseProduct = true;
                product.isInteractable = true;
                product.ReplaceWorldPosition(position);
                product.ReplaceWorldRotation(dropOrigin.rotation);
                product.isProductPlacementDirty = true;
                _events.EmitAudio(AudioCueId.Drop);
            }
        }

        private static void ValidatePlacementState(GameEntity product)
        {
            if (product.hasDeliverySlotIndex || product.hasStorageSlotIndex ||
                product.hasOrderLineEntityId || product.hasLoadingSlotIndex)
            {
                throw new InvalidOperationException(
                    $"Carried product {product.EntityId} still contains slot placement state.");
            }
        }
    }
}
