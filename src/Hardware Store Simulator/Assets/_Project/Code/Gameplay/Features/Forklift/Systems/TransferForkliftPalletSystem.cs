using System;
using Entitas;
using HardwareStore.Gameplay.Common.Physics;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Forklift.Systems
{
    public sealed class TransferForkliftPalletSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IForkliftMotionService _motion;
        private readonly float _transferDistanceSquared;
        private readonly float _transferHeightTolerance;
        private readonly float _minimumTransferAlignmentDot;
        private readonly IGroup<GameEntity> _forklifts;
        private readonly IGroup<GameEntity> _bays;
        private readonly IGroup<GameEntity> _bayPallets;
        private readonly IGroup<InputEntity> _inputs;

        public TransferForkliftPalletSystem(GameContext gameContext,
            InputContext inputContext, IForkliftMotionService motion,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _motion = motion;
            _transferDistanceSquared =
                staticData.Forklift.TransferDistance *
                staticData.Forklift.TransferDistance;
            _transferHeightTolerance =
                staticData.Forklift.TransferHeightTolerance;
            _minimumTransferAlignmentDot = Mathf.Cos(
                staticData.Forklift.TransferMaxAlignmentAngle *
                Mathf.Deg2Rad);
            _forklifts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Forklift,
                    GameMatcher.EntityId,
                    GameMatcher.ForkliftStoreEntityId,
                    GameMatcher.ForkliftDriverEntityId,
                    GameMatcher.CargoAnchor)
                .NoneOf(GameMatcher.Destructed));
            _bays = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.PalletBay,
                    GameMatcher.EntityId,
                    GameMatcher.OccupiedPalletSlotCount,
                    GameMatcher.Slots)
                .NoneOf(GameMatcher.Destructed));
            _bayPallets = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Pallet,
                    GameMatcher.EntityId,
                    GameMatcher.PalletStoreEntityId,
                    GameMatcher.PalletBayEntityId,
                    GameMatcher.PalletBaySlotIndex,
                    GameMatcher.Transform)
                .NoneOf(GameMatcher.Destructed));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(
                InputMatcher.InputState,
                InputMatcher.ForkliftTransferPressed));
        }

        public void Execute()
        {
            foreach (InputEntity ignored in _inputs)
            foreach (GameEntity forklift in _forklifts)
            {
                ValidateDriver(forklift);
                GameEntity carriedPallet =
                    _gameContext.GetEntityWithForkliftCarrierEntityId(
                        forklift.EntityId);
                if (carriedPallet != null)
                {
                    TryPlacePallet(forklift, carriedPallet);
                    continue;
                }

                TryPickUpPallet(forklift);
            }
        }

        private void TryPlacePallet(GameEntity forklift, GameEntity pallet)
        {
            ValidateCarriedPallet(forklift, pallet);
            if (!TryFindClosestFreeSlot(
                    forklift,
                    out GameEntity targetBay,
                    out int targetSlotIndex))
            {
                return;
            }

            pallet.RemoveForkliftCarrierEntityId();
            pallet.AddPalletBayEntityId(targetBay.EntityId);
            pallet.AddPalletBaySlotIndex(targetSlotIndex);
            targetBay.ReplaceOccupiedPalletSlotCount(
                checked(targetBay.OccupiedPalletSlotCount + 1));
            _motion.PlacePallet(
                pallet.Transform,
                targetBay.Slots[targetSlotIndex]);
        }

        private void TryPickUpPallet(GameEntity forklift)
        {
            if (!TryFindClosestBayPallet(forklift, out GameEntity pallet,
                    out GameEntity sourceBay))
            {
                return;
            }

            bool[] occupiedSlots = ResolveOccupiedSlots(sourceBay);
            if (!occupiedSlots[pallet.PalletBaySlotIndex])
            {
                throw new InvalidOperationException(
                    $"Pallet {pallet.EntityId} is not registered in its bay slot.");
            }

            pallet.RemovePalletBaySlotIndex();
            pallet.RemovePalletBayEntityId();
            pallet.AddForkliftCarrierEntityId(forklift.EntityId);
            sourceBay.ReplaceOccupiedPalletSlotCount(
                checked(sourceBay.OccupiedPalletSlotCount - 1));
            _motion.PlacePallet(pallet.Transform, forklift.CargoAnchor);
        }

        private bool TryFindClosestFreeSlot(GameEntity forklift,
            out GameEntity targetBay, out int targetSlotIndex)
        {
            targetBay = null;
            targetSlotIndex = -1;
            float bestDistanceSquared = float.PositiveInfinity;

            foreach (GameEntity bay in _bays)
            {
                ValidateBay(bay);
                if (ResolveBayStoreEntityId(bay) !=
                    forklift.ForkliftStoreEntityId)
                {
                    continue;
                }

                bool[] occupiedSlots = ResolveOccupiedSlots(bay);
                for (int index = 0; index < bay.Slots.Length; index++)
                {
                    if (occupiedSlots[index])
                        continue;

                    if (!TryResolveTransferDistanceSquared(
                            forklift.CargoAnchor,
                            bay.Slots[index],
                            out float distanceSquared) ||
                        !IsBetterCandidate(distanceSquared, bay.EntityId, index,
                            bestDistanceSquared, targetBay, targetSlotIndex))
                    {
                        continue;
                    }

                    bestDistanceSquared = distanceSquared;
                    targetBay = bay;
                    targetSlotIndex = index;
                }
            }

            return targetBay != null;
        }

        private bool TryFindClosestBayPallet(GameEntity forklift,
            out GameEntity targetPallet, out GameEntity sourceBay)
        {
            targetPallet = null;
            sourceBay = null;
            float bestDistanceSquared = float.PositiveInfinity;

            foreach (GameEntity pallet in _bayPallets)
            {
                if (pallet.PalletStoreEntityId !=
                    forklift.ForkliftStoreEntityId ||
                    pallet.hasForkliftCarrierEntityId)
                {
                    continue;
                }

                GameEntity bay = _gameContext.GetEntityWithEntityId(
                    pallet.PalletBayEntityId);
                ValidateBayPallet(pallet, bay);
                if (ResolveBayStoreEntityId(bay) !=
                    forklift.ForkliftStoreEntityId)
                {
                    throw new InvalidOperationException(
                        $"Pallet {pallet.EntityId} and bay {bay.EntityId} belong to different stores.");
                }

                Transform slot = bay.Slots[pallet.PalletBaySlotIndex];
                if (!TryResolveTransferDistanceSquared(
                        forklift.CargoAnchor,
                        slot,
                        out float distanceSquared) ||
                    !IsBetterCandidate(
                        distanceSquared,
                        pallet.EntityId,
                        pallet.PalletBaySlotIndex,
                        bestDistanceSquared,
                        targetPallet,
                        targetPallet == null
                            ? -1
                            : targetPallet.PalletBaySlotIndex))
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                targetPallet = pallet;
                sourceBay = bay;
            }

            return targetPallet != null;
        }

        private bool TryResolveTransferDistanceSquared(Transform cargoAnchor,
            Transform slot, out float planarDistanceSquared)
        {
            Vector3 offset = slot.position - cargoAnchor.position;
            Vector3 planarOffset = Vector3.ProjectOnPlane(offset, Vector3.up);
            planarDistanceSquared = planarOffset.sqrMagnitude;
            if (Mathf.Abs(offset.y) > _transferHeightTolerance ||
                planarDistanceSquared > _transferDistanceSquared)
            {
                return false;
            }

            Vector3 cargoForward = Vector3.ProjectOnPlane(
                cargoAnchor.forward, Vector3.up);
            Vector3 slotForward = Vector3.ProjectOnPlane(
                slot.forward, Vector3.up);
            if (cargoForward.sqrMagnitude <= Mathf.Epsilon ||
                slotForward.sqrMagnitude <= Mathf.Epsilon)
            {
                throw new InvalidOperationException(
                    "Forklift transfer anchors must have a horizontal forward direction.");
            }

            Vector3 normalizedCargoForward = cargoForward.normalized;
            if (Vector3.Dot(
                    normalizedCargoForward,
                    slotForward.normalized) < _minimumTransferAlignmentDot)
            {
                return false;
            }

            return planarDistanceSquared <= Mathf.Epsilon ||
                   Vector3.Dot(
                       normalizedCargoForward,
                       planarOffset.normalized) >=
                   _minimumTransferAlignmentDot;
        }

        private bool[] ResolveOccupiedSlots(GameEntity bay)
        {
            var occupiedSlots = new bool[bay.Slots.Length];
            int occupiedCount = 0;
            foreach (GameEntity pallet in
                     _gameContext.GetEntitiesWithPalletBayEntityId(bay.EntityId))
            {
                if (pallet.isDestructed)
                    continue;
                ValidateBayPallet(pallet, bay);
                int slotIndex = pallet.PalletBaySlotIndex;
                if (occupiedSlots[slotIndex])
                {
                    throw new InvalidOperationException(
                        $"Pallet bay {bay.EntityId} has duplicate ownership of slot {slotIndex}.");
                }

                occupiedSlots[slotIndex] = true;
                occupiedCount++;
            }

            if (occupiedCount != bay.OccupiedPalletSlotCount)
            {
                throw new InvalidOperationException(
                    $"Pallet bay {bay.EntityId} reports {bay.OccupiedPalletSlotCount} " +
                    $"occupied slots, found {occupiedCount}.");
            }

            return occupiedSlots;
        }

        private void ValidateDriver(GameEntity forklift)
        {
            GameEntity driver = _gameContext.GetEntityWithEntityId(
                forklift.ForkliftDriverEntityId);
            if (driver == null || !driver.isPlayer ||
                !driver.isDrivingForklift || driver.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Forklift {forklift.EntityId} cannot transfer a pallet without a driver.");
            }
        }

        private static void ValidateCarriedPallet(GameEntity forklift,
            GameEntity pallet)
        {
            if (pallet == null || !pallet.isPallet || !pallet.hasEntityId ||
                !pallet.hasPalletStoreEntityId ||
                pallet.PalletStoreEntityId != forklift.ForkliftStoreEntityId ||
                !pallet.hasForkliftCarrierEntityId ||
                pallet.ForkliftCarrierEntityId != forklift.EntityId ||
                pallet.hasPalletBayEntityId || pallet.hasPalletBaySlotIndex ||
                !pallet.hasTransform || pallet.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Forklift {forklift.EntityId} carries an invalid pallet.");
            }
        }

        private static void ValidateBay(GameEntity bay)
        {
            if (bay == null || !bay.isPalletBay || !bay.hasEntityId ||
                !bay.hasSlots || bay.Slots.Length == 0 ||
                !bay.hasOccupiedPalletSlotCount ||
                bay.OccupiedPalletSlotCount < 0 ||
                bay.OccupiedPalletSlotCount > bay.Slots.Length ||
                bay.isDestructed)
            {
                throw new InvalidOperationException(
                    "Pallet bay has invalid runtime state.");
            }

            foreach (Transform slot in bay.Slots)
            {
                if (slot == null)
                {
                    throw new InvalidOperationException(
                        $"Pallet bay {bay.EntityId} has a missing slot.");
                }
            }
        }

        private static void ValidateBayPallet(GameEntity pallet,
            GameEntity bay)
        {
            ValidateBay(bay);
            if (pallet == null || !pallet.isPallet || !pallet.hasEntityId ||
                !pallet.hasPalletBayEntityId ||
                pallet.PalletBayEntityId != bay.EntityId ||
                !pallet.hasPalletBaySlotIndex ||
                pallet.PalletBaySlotIndex < 0 ||
                pallet.PalletBaySlotIndex >= bay.Slots.Length ||
                pallet.hasForkliftCarrierEntityId || !pallet.hasTransform ||
                pallet.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Pallet bay {bay.EntityId} contains invalid pallet state.");
            }
        }

        private static int ResolveBayStoreEntityId(GameEntity bay)
        {
            bool isTruckBay = bay.isFreightTruck &&
                              bay.hasFreightTruckStoreEntityId;
            bool isStagingBay = bay.isFreightStagingZone &&
                                bay.hasFreightStagingZoneStoreEntityId;
            if (isTruckBay == isStagingBay)
            {
                throw new InvalidOperationException(
                    $"Pallet bay {bay.EntityId} must have exactly one freight bay role.");
            }

            return isTruckBay
                ? bay.FreightTruckStoreEntityId
                : bay.FreightStagingZoneStoreEntityId;
        }

        private static bool IsBetterCandidate(float distanceSquared,
            int entityId, int slotIndex, float bestDistanceSquared,
            GameEntity bestEntity, int bestSlotIndex)
        {
            if (distanceSquared < bestDistanceSquared)
                return true;
            if (distanceSquared > bestDistanceSquared)
                return false;
            if (bestEntity == null || entityId != bestEntity.EntityId)
                return bestEntity == null || entityId < bestEntity.EntityId;
            return slotIndex < bestSlotIndex;
        }
    }
}
