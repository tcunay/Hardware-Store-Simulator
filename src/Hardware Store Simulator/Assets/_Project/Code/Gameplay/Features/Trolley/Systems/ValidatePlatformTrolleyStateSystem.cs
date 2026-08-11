using System;
using Entitas;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Trolley.Systems
{
    public sealed class ValidatePlatformTrolleyStateSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _trolleys;

        public ValidatePlatformTrolleyStateSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _trolleys = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.PlatformTrolley,
                    GameMatcher.EntityId,
                    GameMatcher.TrolleyStoreEntityId,
                    GameMatcher.TrolleyCapacity,
                    GameMatcher.OccupiedTrolleySlotCount,
                    GameMatcher.TrolleyMovementSpeed,
                    GameMatcher.TrolleyFollowDistance,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody,
                    GameMatcher.Slots)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity trolley in _trolleys)
            {
                ValidateConfiguration(trolley);
                int cargoCount = ValidateCargo(trolley);
                if (cargoCount != trolley.OccupiedTrolleySlotCount)
                {
                    throw new InvalidOperationException(
                        $"Trolley {trolley.EntityId} reports " +
                        $"{trolley.OccupiedTrolleySlotCount} occupied slots, found " +
                        $"{cargoCount} cargo entities.");
                }

                ValidatePusher(trolley);
            }
        }

        private void ValidateConfiguration(GameEntity trolley)
        {
            GameEntity store =
                _gameContext.GetEntityWithEntityId(trolley.TrolleyStoreEntityId);
            if (store == null || !store.isStore || store.isDestructed ||
                trolley.TrolleyCapacity <= 0 ||
                trolley.Slots.Length != trolley.TrolleyCapacity ||
                trolley.OccupiedTrolleySlotCount < 0 ||
                trolley.OccupiedTrolleySlotCount > trolley.TrolleyCapacity ||
                trolley.TrolleyMovementSpeed <= 0f ||
                trolley.TrolleyFollowDistance <= 0f ||
                !IsFinite(trolley.TrolleyMovementSpeed) ||
                !IsFinite(trolley.TrolleyFollowDistance) ||
                !trolley.Rigidbody.isKinematic || trolley.Rigidbody.useGravity)
            {
                throw new InvalidOperationException(
                    $"Platform trolley {trolley.EntityId} has invalid configuration.");
            }

            for (int index = 0; index < trolley.Slots.Length; index++)
            {
                if (trolley.Slots[index] == null)
                    throw new InvalidOperationException(
                        $"Platform trolley {trolley.EntityId} has a missing slot {index}.");
            }
        }

        private int ValidateCargo(GameEntity trolley)
        {
            var occupiedSlots = new bool[trolley.TrolleyCapacity];
            int cargoCount = 0;
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithTrolleyEntityId(trolley.EntityId))
            {
                bool hasInboundReservation =
                    product.isInboundProduct && !product.isInStock &&
                    product.hasDeliveryEntityId &&
                    product.hasReservedDeliverySlotIndex &&
                    !product.hasStorageZoneEntityId &&
                    !product.hasReservedStorageSlotIndex &&
                    !product.hasReservedOrderLineEntityId;
                bool hasStockReservation =
                    product.isInStock && !product.isInboundProduct &&
                    product.hasStorageZoneEntityId &&
                    product.hasReservedStorageSlotIndex &&
                    product.hasReservedOrderLineEntityId &&
                    !product.hasDeliveryEntityId &&
                    !product.hasReservedDeliverySlotIndex;
                if (product.isDestructed || !product.isProduct ||
                    !product.hasEntityId ||
                    !product.hasTrolleySlotIndex ||
                    (!hasInboundReservation && !hasStockReservation) ||
                    product.TrolleySlotIndex < 0 ||
                    product.TrolleySlotIndex >= occupiedSlots.Length ||
                    occupiedSlots[product.TrolleySlotIndex] ||
                    product.hasCarrierEntityId || product.hasDeliverySlotIndex ||
                    product.hasStorageSlotIndex || product.hasOrderLineEntityId ||
                    product.hasLoadingSlotIndex || product.isLooseProduct ||
                    product.isLoaded || product.hasWorldPosition ||
                    product.hasWorldRotation || !product.isInteractable)
                {
                    throw new InvalidOperationException(
                        $"Trolley {trolley.EntityId} contains invalid cargo state.");
                }

                occupiedSlots[product.TrolleySlotIndex] = true;
                cargoCount++;
            }

            return cargoCount;
        }

        private void ValidatePusher(GameEntity trolley)
        {
            if (!trolley.hasTrolleyPusherEntityId)
            {
                if (!trolley.isInteractable)
                    throw new InvalidOperationException(
                        $"Parked trolley {trolley.EntityId} is not interactable.");
                return;
            }

            GameEntity player =
                _gameContext.GetEntityWithEntityId(trolley.TrolleyPusherEntityId);
            if (player == null || !player.isPlayer || player.isDestructed ||
                !player.isHandsOccupied || !player.isPushingTrolley ||
                player.isCarryingProduct || trolley.isInteractable)
            {
                throw new InvalidOperationException(
                    $"Trolley {trolley.EntityId} has an invalid pusher relation.");
            }
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
