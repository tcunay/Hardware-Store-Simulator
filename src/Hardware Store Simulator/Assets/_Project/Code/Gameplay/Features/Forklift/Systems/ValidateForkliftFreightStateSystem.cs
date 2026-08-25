using System;
using Entitas;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Forklift.Systems
{
    public sealed class ValidateForkliftFreightStateSystem : IExecuteSystem
    {
        private const float HeightTolerance = 0.001f;

        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _forklifts;
        private readonly IGroup<GameEntity> _bays;
        private readonly IGroup<GameEntity> _pallets;

        public ValidateForkliftFreightStateSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _forklifts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Forklift,
                    GameMatcher.EntityId)
                .NoneOf(GameMatcher.Destructed));
            _bays = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.PalletBay,
                    GameMatcher.EntityId)
                .NoneOf(GameMatcher.Destructed));
            _pallets = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Pallet,
                    GameMatcher.EntityId)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity forklift in _forklifts)
                ValidateForklift(forklift);
            foreach (GameEntity bay in _bays)
                ValidateBay(bay);
            foreach (GameEntity pallet in _pallets)
                ValidatePallet(pallet);
        }

        private void ValidateForklift(GameEntity forklift)
        {
            if (!forklift.hasForkliftStoreEntityId ||
                !forklift.hasForkliftForkHeight ||
                !forklift.hasForkliftMinForkHeight ||
                !forklift.hasForkliftMaxForkHeight ||
                !forklift.hasForkliftLiftSpeed ||
                !forklift.hasForkliftForwardSpeed ||
                !forklift.hasForkliftReverseSpeed ||
                !forklift.hasForkliftSteeringSpeed ||
                !forklift.hasTransform || !forklift.hasRigidbody ||
                !forklift.hasColliders || !forklift.hasDriverSeatAnchor ||
                !forklift.hasDriverExitAnchor || !forklift.hasLiftTransform ||
                !forklift.hasCargoAnchor ||
                !IsFinite(forklift.ForkliftForkHeight) ||
                !IsFinite(forklift.ForkliftMinForkHeight) ||
                !IsFinite(forklift.ForkliftMaxForkHeight) ||
                !IsFinitePositive(forklift.ForkliftLiftSpeed) ||
                !IsFinitePositive(forklift.ForkliftForwardSpeed) ||
                !IsFinitePositive(forklift.ForkliftReverseSpeed) ||
                !IsFinitePositive(forklift.ForkliftSteeringSpeed) ||
                forklift.ForkliftMinForkHeight < 0f ||
                forklift.ForkliftMaxForkHeight <=
                forklift.ForkliftMinForkHeight ||
                forklift.ForkliftForkHeight <
                forklift.ForkliftMinForkHeight - HeightTolerance ||
                forklift.ForkliftForkHeight >
                forklift.ForkliftMaxForkHeight + HeightTolerance ||
                Mathf.Abs(forklift.LiftTransform.localPosition.y -
                          forklift.ForkliftForkHeight) > HeightTolerance ||
                forklift.Rigidbody.transform != forklift.Transform ||
                !forklift.Rigidbody.isKinematic ||
                forklift.Rigidbody.interpolation != RigidbodyInterpolation.None ||
                forklift.Rigidbody.useGravity ||
                !forklift.Rigidbody.detectCollisions ||
                forklift.Colliders.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Forklift {forklift.EntityId} has invalid runtime configuration.");
            }

            ValidateDriverRelation(forklift);
            ValidateCarrierRelation(forklift);
        }

        private void ValidateDriverRelation(GameEntity forklift)
        {
            if (!forklift.hasForkliftDriverEntityId)
            {
                if (!forklift.isInteractable)
                {
                    throw new InvalidOperationException(
                        $"Parked forklift {forklift.EntityId} is not interactable.");
                }

                return;
            }

            GameEntity driver = _gameContext.GetEntityWithEntityId(
                forklift.ForkliftDriverEntityId);
            if (driver == null || !driver.isPlayer || !driver.hasEntityId ||
                !driver.isDrivingForklift || driver.isHandsOccupied ||
                driver.isModalOpen || driver.isDestructed ||
                !driver.hasCharacterController ||
                driver.CharacterController.enabled || forklift.isInteractable)
            {
                throw new InvalidOperationException(
                    $"Forklift {forklift.EntityId} has an invalid driver relation.");
            }
        }

        private void ValidateCarrierRelation(GameEntity forklift)
        {
            GameEntity pallet =
                _gameContext.GetEntityWithForkliftCarrierEntityId(
                    forklift.EntityId);
            if (pallet == null)
                return;
            if (!pallet.isPallet || pallet.isDestructed ||
                !pallet.hasPalletStoreEntityId ||
                pallet.PalletStoreEntityId != forklift.ForkliftStoreEntityId ||
                pallet.hasPalletBayEntityId || pallet.hasPalletBaySlotIndex)
            {
                throw new InvalidOperationException(
                    $"Forklift {forklift.EntityId} carries invalid pallet state.");
            }
        }

        private void ValidateBay(GameEntity bay)
        {
            if (!bay.hasSlots || bay.Slots.Length == 0 ||
                !bay.hasOccupiedPalletSlotCount ||
                bay.OccupiedPalletSlotCount < 0 ||
                bay.OccupiedPalletSlotCount > bay.Slots.Length)
            {
                throw new InvalidOperationException(
                    $"Pallet bay {bay.EntityId} has invalid configuration.");
            }

            bool truckRole = bay.isFreightTruck &&
                             bay.hasFreightTruckStoreEntityId;
            bool stagingRole = bay.isFreightStagingZone &&
                               bay.hasFreightStagingZoneStoreEntityId;
            if (truckRole == stagingRole)
            {
                throw new InvalidOperationException(
                    $"Pallet bay {bay.EntityId} must have exactly one freight role.");
            }

            var occupiedSlots = new bool[bay.Slots.Length];
            int occupiedCount = 0;
            for (int index = 0; index < bay.Slots.Length; index++)
            {
                if (bay.Slots[index] == null)
                {
                    throw new InvalidOperationException(
                        $"Pallet bay {bay.EntityId} has a missing slot {index}.");
                }
            }

            foreach (GameEntity pallet in
                     _gameContext.GetEntitiesWithPalletBayEntityId(bay.EntityId))
            {
                if (pallet.isDestructed)
                    continue;
                if (!pallet.isPallet || !pallet.hasPalletBaySlotIndex ||
                    pallet.PalletBaySlotIndex < 0 ||
                    pallet.PalletBaySlotIndex >= occupiedSlots.Length ||
                    occupiedSlots[pallet.PalletBaySlotIndex] ||
                    pallet.hasForkliftCarrierEntityId)
                {
                    throw new InvalidOperationException(
                        $"Pallet bay {bay.EntityId} contains invalid slot ownership.");
                }

                occupiedSlots[pallet.PalletBaySlotIndex] = true;
                occupiedCount++;
            }

            if (occupiedCount != bay.OccupiedPalletSlotCount)
            {
                throw new InvalidOperationException(
                    $"Pallet bay {bay.EntityId} reports " +
                    $"{bay.OccupiedPalletSlotCount} occupied slots, found {occupiedCount}.");
            }
        }

        private void ValidatePallet(GameEntity pallet)
        {
            bool inBay = pallet.hasPalletBayEntityId;
            bool onForklift = pallet.hasForkliftCarrierEntityId;
            if (!pallet.hasPalletStoreEntityId || !pallet.hasTransform ||
                inBay == onForklift ||
                inBay != pallet.hasPalletBaySlotIndex)
            {
                throw new InvalidOperationException(
                    $"Pallet {pallet.EntityId} violates bay/carrier exclusivity.");
            }

            if (onForklift)
            {
                GameEntity forklift = _gameContext.GetEntityWithEntityId(
                    pallet.ForkliftCarrierEntityId);
                if (forklift == null || !forklift.isForklift ||
                    !forklift.hasForkliftStoreEntityId ||
                    forklift.ForkliftStoreEntityId !=
                    pallet.PalletStoreEntityId || forklift.isDestructed)
                {
                    throw new InvalidOperationException(
                        $"Pallet {pallet.EntityId} references an invalid forklift.");
                }

                return;
            }

            GameEntity bay = _gameContext.GetEntityWithEntityId(
                pallet.PalletBayEntityId);
            if (bay == null || !bay.isPalletBay || !bay.hasSlots ||
                pallet.PalletBaySlotIndex < 0 ||
                pallet.PalletBaySlotIndex >= bay.Slots.Length ||
                bay.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Pallet {pallet.EntityId} references an invalid bay.");
            }

            int bayStoreEntityId = bay.isFreightTruck
                ? bay.FreightTruckStoreEntityId
                : bay.FreightStagingZoneStoreEntityId;
            if (bayStoreEntityId != pallet.PalletStoreEntityId)
            {
                throw new InvalidOperationException(
                    $"Pallet {pallet.EntityId} and bay {bay.EntityId} belong to different stores.");
            }

            if (pallet.isInboundPallet && bay.isFreightStagingZone)
            {
                throw new InvalidOperationException(
                    $"Inbound pallet {pallet.EntityId} was not completed in staging.");
            }
        }

        private static bool IsFinitePositive(float value) =>
            IsFinite(value) && value > 0f;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
