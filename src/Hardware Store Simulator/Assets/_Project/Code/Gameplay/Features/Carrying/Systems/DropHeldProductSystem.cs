using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Common.Entity;
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
                GameEntity product = GetRequiredHeldProduct(player.HeldProductId);
                Transform dropOrigin = player.DropOrigin;
                Vector3 position = dropOrigin.position +
                                   dropOrigin.forward * product.DropForwardDistance;

                player.RemoveHeldProductId();
                product.isCarried = false;
                product.isLooseProduct = true;
                product.isInteractable = true;
                product.ReplaceWorldPosition(position);
                product.ReplaceWorldRotation(dropOrigin.rotation);
                product.isProductPlacementDirty = true;
                _events.EmitAudio(AudioCueId.Drop);
            }
        }

        private GameEntity GetRequiredHeldProduct(int productEntityId)
        {
            GameEntity product = _gameContext.GetRequiredEntity(
                productEntityId,
                "player held product");
            if (!product.isProduct || !product.isCarried || !product.hasDropForwardDistance)
                throw new InvalidOperationException(
                    $"Entity {productEntityId} is not a fully configured carried product.");
            if (product.hasDeliverySlotIndex || product.hasStorageSlotIndex ||
                product.hasLoadingZoneEntityId || product.hasLoadingSlotIndex)
            {
                throw new InvalidOperationException(
                    $"Carried product {productEntityId} still contains slot placement state.");
            }

            return product;
        }
    }
}
