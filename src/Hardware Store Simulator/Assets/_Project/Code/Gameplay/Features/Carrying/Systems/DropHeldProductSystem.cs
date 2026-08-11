using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Physics;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Carrying.Systems
{
    public sealed class DropHeldProductSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IProductDropPhysicsService _physics;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;
        private readonly List<GameEntity> _buffer = new(4);

        public DropHeldProductSystem(
            GameContext gameContext,
            InputContext inputContext,
            IGameEventFactory events,
            IProductDropPhysicsService physics)
        {
            _gameContext = gameContext;
            _events = events;
            _physics = physics;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.EntityId,
                GameMatcher.HandsOccupied,
                GameMatcher.CarryingProduct,
                GameMatcher.DropOrigin,
                GameMatcher.CharacterController)
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
                if (!_physics.TryGetSafeDropPosition(
                        dropOrigin.position,
                        dropOrigin.forward,
                        product.DropForwardDistance,
                        product.ProductDropCollisionRadius,
                        player.CharacterController,
                        out Vector3 position))
                {
                    _events.EmitNotification(LocalizedTexts.Text(
                        LocalizationKey.NotificationProductDropBlocked));
                    continue;
                }

                product.RemoveCarrierEntityId();
                player.isHandsOccupied = false;
                player.isCarryingProduct = false;
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
            if (!product.isProduct || product.isDestructed || !product.hasEntityId ||
                !product.hasDropForwardDistance || !product.hasProductDropCollisionRadius)
            {
                throw new InvalidOperationException(
                    "CarrierEntityId index returned an invalid droppable product.");
            }

            if (product.hasDeliverySlotIndex || product.hasStorageSlotIndex ||
                product.hasOrderLineEntityId || product.hasLoadingSlotIndex ||
                product.hasTrolleyEntityId || product.hasTrolleySlotIndex)
            {
                throw new InvalidOperationException(
                    $"Carried product {product.EntityId} still contains slot placement state.");
            }

            if (product.isInboundProduct)
            {
                if (!product.hasDeliveryEntityId ||
                    !product.hasReservedDeliverySlotIndex ||
                    product.isInStock || product.hasStorageZoneEntityId ||
                    product.hasReservedStorageSlotIndex ||
                    product.hasReservedOrderLineEntityId)
                {
                    throw new InvalidOperationException(
                        $"Carried inbound product {product.EntityId} has invalid slot reservation state.");
                }

                return;
            }

            if (!product.isInStock || !product.hasStorageZoneEntityId ||
                !product.hasReservedStorageSlotIndex ||
                !product.hasReservedOrderLineEntityId ||
                product.hasDeliveryEntityId || product.hasReservedDeliverySlotIndex)
            {
                throw new InvalidOperationException(
                    $"Carried stock product {product.EntityId} has invalid slot reservation state.");
            }
        }
    }
}
