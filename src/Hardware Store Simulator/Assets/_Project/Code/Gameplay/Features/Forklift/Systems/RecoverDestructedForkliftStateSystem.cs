using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Physics;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Forklift.Systems
{
    public sealed class RecoverDestructedForkliftStateSystem :
        IExecuteSystem,
        ICleanupSystem
    {
        private readonly GameContext _gameContext;
        private readonly IForkliftMotionService _motion;
        private readonly IGroup<GameEntity> _forklifts;
        private readonly IGroup<GameEntity> _drivers;
        private readonly IGroup<GameEntity> _pallets;
        private readonly IGroup<GameEntity> _bays;
        private readonly IGroup<GameEntity> _driverRelations;
        private readonly IGroup<GameEntity> _carrierRelations;
        private readonly IGroup<GameEntity> _bayRelations;
        private readonly IGroup<GameEntity> _baySlotRelations;

        private readonly List<GameEntity> _forkliftBuffer = new(4);
        private readonly List<GameEntity> _driverBuffer = new(2);
        private readonly List<GameEntity> _palletBuffer = new(16);
        private readonly List<GameEntity> _bayBuffer = new(4);
        private readonly List<GameEntity> _relationBuffer = new(16);
        private readonly List<GameEntity> _recoveryPallets = new(8);
        private readonly HashSet<int> _queuedPalletIds = new();
        private readonly HashSet<long> _claimedLiveBaySlots = new();
        private readonly Dictionary<int, bool[]> _occupiedSlotsByBayId =
            new();
        private readonly Dictionary<int, int> _storeByBayId = new();

        public RecoverDestructedForkliftStateSystem(GameContext gameContext,
            IForkliftMotionService motion)
        {
            _gameContext = gameContext;
            _motion = motion;
            _forklifts = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Forklift,
                GameMatcher.EntityId));
            _drivers = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.EntityId,
                GameMatcher.DrivingForklift));
            _pallets = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Pallet,
                GameMatcher.EntityId));
            _bays = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.PalletBay,
                GameMatcher.EntityId));
            _driverRelations = gameContext.GetGroup(
                GameMatcher.ForkliftDriverEntityId);
            _carrierRelations = gameContext.GetGroup(
                GameMatcher.ForkliftCarrierEntityId);
            _bayRelations = gameContext.GetGroup(
                GameMatcher.PalletBayEntityId);
            _baySlotRelations = gameContext.GetGroup(
                GameMatcher.PalletBaySlotIndex);
        }

        public void Execute() => Reconcile();

        public void Cleanup() => Reconcile();

        private void Reconcile()
        {
            SnapshotEntities();
            ValidateLiveGraphContracts();

            // Relations are planned from stable snapshots before any endpoint
            // is removed. Each phase leaves a state valid for the next phase.
            ReconcileDriverRelations();
            PrepareLiveBays();
            DetachInvalidRelationOwners();
            ReconcileLivePalletPlacements();
            RecalculateLiveBayCounts();
            RecoverOrDestroyOrphanedPallets();
            RecalculateLiveBayCounts();
            FinalizeDestructedEndpoints();
        }

        private void ValidateLiveGraphContracts()
        {
            ValidatePlacementRelationOwnerContracts();
            ValidateDriverContracts();
            ValidateLiveBayContracts();
            ValidateLivePalletContracts();
        }

        private void ValidatePlacementRelationOwnerContracts()
        {
            ValidateLivePalletRelationOwners(
                _carrierRelations, nameof(GameEntity.ForkliftCarrierEntityId));
            ValidateLivePalletRelationOwners(
                _bayRelations, nameof(GameEntity.PalletBayEntityId));
            ValidateLivePalletRelationOwners(
                _baySlotRelations, nameof(GameEntity.PalletBaySlotIndex));
        }

        private void ValidateLivePalletRelationOwners(
            IGroup<GameEntity> relationOwners, string relationName)
        {
            foreach (GameEntity relationOwner in
                     relationOwners.GetEntities(_relationBuffer))
            {
                if (relationOwner.isDestructed)
                    continue;
                if (!relationOwner.isPallet || !relationOwner.hasEntityId)
                {
                    throw new InvalidOperationException(
                        $"Live {relationName} owner must be an identified pallet.");
                }
            }
        }

        private void ValidateDriverContracts()
        {
            foreach (GameEntity relationOwner in
                     _driverRelations.GetEntities(_relationBuffer))
            {
                GameEntity driver = _gameContext.GetEntityWithEntityId(
                    relationOwner.ForkliftDriverEntityId);
                ValidateDriverRelationRoles(relationOwner, driver);
                if (IsValidLiveDriverRelation(relationOwner, driver))
                    continue;
                if (driver != null && !driver.isDestructed)
                    ValidateLiveDriverRecoveryPrerequisites(driver);
                if (!relationOwner.isDestructed && driver != null &&
                    !driver.isDestructed)
                {
                    throw new InvalidOperationException(
                        $"Live forklift {relationOwner.EntityId} has an invalid " +
                        "live driver relation.");
                }
            }

            foreach (GameEntity driver in _driverBuffer)
            {
                GameEntity forklift =
                    _gameContext.GetEntityWithForkliftDriverEntityId(
                        driver.EntityId);
                if (forklift != null && !forklift.isForklift)
                {
                    throw new InvalidOperationException(
                        $"Entity {forklift.EntityId} owns a forklift driver " +
                        "relation without the Forklift role.");
                }
                if (IsValidLiveDriverRelation(forklift, driver))
                    continue;
                if (!driver.isDestructed)
                    ValidateLiveDriverRecoveryPrerequisites(driver);
                if (!driver.isDestructed && forklift != null &&
                    !forklift.isDestructed)
                {
                    throw new InvalidOperationException(
                        $"Live player {driver.EntityId} has an invalid live " +
                        "forklift relation.");
                }
            }
        }

        private static void ValidateLiveDriverRecoveryPrerequisites(
            GameEntity driver)
        {
            if (!driver.hasTransform || driver.Transform == null ||
                !driver.Transform.gameObject.activeInHierarchy ||
                !driver.hasCharacterController ||
                driver.CharacterController == null ||
                !driver.CharacterController.gameObject.activeInHierarchy ||
                driver.isDrivingForklift &&
                driver.CharacterController.enabled)
            {
                throw new InvalidOperationException(
                    $"Live forklift driver {driver.EntityId} cannot be safely " +
                    "released from the vehicle.");
            }
        }

        private void ValidateLiveBayContracts()
        {
            foreach (GameEntity bay in _bayBuffer)
            {
                if (!bay.isDestructed &&
                    !TryGetValidBayStoreEntityId(bay, out int ignored))
                {
                    throw new InvalidOperationException(
                        $"Live pallet bay {bay.EntityId} has invalid configuration.");
                }
            }
        }

        private void ValidateLivePalletContracts()
        {
            _claimedLiveBaySlots.Clear();
            foreach (GameEntity pallet in _palletBuffer)
            {
                if (pallet.isDestructed)
                    continue;
                if (!pallet.hasPalletStoreEntityId ||
                    pallet.PalletStoreEntityId <= 0 ||
                    !pallet.hasTransform || pallet.Transform == null ||
                    !pallet.Transform.gameObject.activeInHierarchy)
                {
                    throw new InvalidOperationException(
                        $"Live pallet {pallet.EntityId} has invalid base configuration.");
                }

                bool hasCarrier = pallet.hasForkliftCarrierEntityId;
                bool hasBay = pallet.hasPalletBayEntityId;
                bool hasSlot = pallet.hasPalletBaySlotIndex;
                if (hasCarrier == hasBay || hasBay != hasSlot)
                {
                    throw new InvalidOperationException(
                        $"Live pallet {pallet.EntityId} violates carrier/bay XOR.");
                }

                if (hasCarrier)
                {
                    ValidateLiveCarrierEndpoint(pallet);
                    continue;
                }

                ValidateLiveBayEndpoint(pallet);
            }
        }

        private void ValidateLiveCarrierEndpoint(GameEntity pallet)
        {
            GameEntity forklift = _gameContext.GetEntityWithEntityId(
                pallet.ForkliftCarrierEntityId);
            if (forklift == null || forklift.isDestructed)
                return;
            if (!forklift.isForklift || !forklift.hasEntityId ||
                !forklift.hasForkliftStoreEntityId ||
                forklift.ForkliftStoreEntityId !=
                pallet.PalletStoreEntityId ||
                !forklift.hasCargoAnchor || forklift.CargoAnchor == null ||
                !forklift.CargoAnchor.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException(
                    $"Live pallet {pallet.EntityId} references an invalid live " +
                    "forklift carrier.");
            }
        }

        private void ValidateLiveBayEndpoint(GameEntity pallet)
        {
            GameEntity bay = _gameContext.GetEntityWithEntityId(
                pallet.PalletBayEntityId);
            if (bay == null || bay.isDestructed)
                return;
            if (!bay.isPalletBay || !bay.hasEntityId ||
                !TryGetValidBayStoreEntityId(bay, out int storeEntityId) ||
                storeEntityId != pallet.PalletStoreEntityId ||
                pallet.PalletBaySlotIndex < 0 ||
                pallet.PalletBaySlotIndex >= bay.Slots.Length)
            {
                throw new InvalidOperationException(
                    $"Live pallet {pallet.EntityId} references an invalid live bay.");
            }

            long slotKey = ((long)bay.EntityId << 32) |
                           (uint)pallet.PalletBaySlotIndex;
            if (!_claimedLiveBaySlots.Add(slotKey))
            {
                throw new InvalidOperationException(
                    $"Live pallet bay {bay.EntityId} has duplicate ownership of " +
                    $"slot {pallet.PalletBaySlotIndex}.");
            }
            if (pallet.isInboundPallet && bay.isFreightStagingZone)
            {
                throw new InvalidOperationException(
                    $"Inbound pallet {pallet.EntityId} is already in staging.");
            }
        }

        private void SnapshotEntities()
        {
            _forklifts.GetEntities(_forkliftBuffer);
            _drivers.GetEntities(_driverBuffer);
            _pallets.GetEntities(_palletBuffer);
            _bays.GetEntities(_bayBuffer);

            _forkliftBuffer.Sort(CompareByEntityId);
            _driverBuffer.Sort(CompareByEntityId);
            _palletBuffer.Sort(CompareByEntityId);
            _bayBuffer.Sort(CompareByEntityId);
        }

        private void ReconcileDriverRelations()
        {
            foreach (GameEntity relationOwner in
                     _driverRelations.GetEntities(_relationBuffer))
            {
                GameEntity driver = _gameContext.GetEntityWithEntityId(
                    relationOwner.ForkliftDriverEntityId);
                ValidateDriverRelationRoles(relationOwner, driver);
                if (IsValidLiveDriverRelation(relationOwner, driver))
                {
                    relationOwner.isInteractable = false;
                    relationOwner.isHighlighted = false;
                    continue;
                }

                if (!ReleaseDriver(driver, relationOwner))
                    continue;

                relationOwner.RemoveForkliftDriverEntityId();
                SetParkedForkliftInteraction(relationOwner);
            }

            foreach (GameEntity driver in _driverBuffer)
            {
                if (!driver.isDrivingForklift)
                    continue;

                GameEntity forklift =
                    _gameContext.GetEntityWithForkliftDriverEntityId(
                        driver.EntityId);
                if (forklift != null && !forklift.isForklift)
                {
                    throw new InvalidOperationException(
                        $"Entity {forklift.EntityId} owns a forklift driver " +
                        "relation without the Forklift role.");
                }
                if (IsValidLiveDriverRelation(forklift, driver))
                    continue;

                if (!ReleaseDriver(driver, forklift))
                    continue;

                if (forklift != null && forklift.hasForkliftDriverEntityId)
                {
                    forklift.RemoveForkliftDriverEntityId();
                    SetParkedForkliftInteraction(forklift);
                }
            }
        }

        private static void ValidateDriverRelationRoles(GameEntity forklift,
            GameEntity driver)
        {
            if (!forklift.isForklift || !forklift.hasEntityId)
            {
                throw new InvalidOperationException(
                    "ForkliftDriverEntityId must be owned by an identified forklift.");
            }
            if (driver != null && (!driver.isPlayer || !driver.hasEntityId))
            {
                throw new InvalidOperationException(
                    $"Forklift {forklift.EntityId} references a non-player driver.");
            }
        }

        private static bool IsValidLiveDriverRelation(GameEntity forklift,
            GameEntity driver) =>
            forklift != null && forklift.isForklift &&
            forklift.hasEntityId && !forklift.isDestructed &&
            forklift.hasForkliftStoreEntityId &&
            forklift.hasForkliftDriverEntityId &&
            driver != null && driver.isPlayer && driver.hasEntityId &&
            !driver.isDestructed && driver.isDrivingForklift &&
            driver.EntityId == forklift.ForkliftDriverEntityId &&
            driver.hasStoreEntityId &&
            driver.StoreEntityId == forklift.ForkliftStoreEntityId &&
            !driver.isHandsOccupied && !driver.isModalOpen &&
            driver.hasTransform && driver.Transform != null &&
            driver.Transform.gameObject.activeInHierarchy &&
            driver.hasCharacterController &&
            driver.CharacterController != null &&
            !driver.CharacterController.enabled;

        private bool ReleaseDriver(GameEntity driver, GameEntity forklift)
        {
            if (driver == null)
                return true;

            if (!driver.isDestructed &&
                !TryRestoreLiveDriverController(driver, forklift))
            {
                return false;
            }

            driver.isDrivingForklift = false;
            if (!driver.isDestructed)
                ResetPlayerState(driver);
            return true;
        }

        private bool TryRestoreLiveDriverController(GameEntity driver,
            GameEntity forklift)
        {
            if (!driver.hasCharacterController ||
                driver.CharacterController == null ||
                !driver.CharacterController.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException(
                    $"Live forklift driver {driver.EntityId} has no active " +
                    "CharacterController.");
            }
            if (driver.CharacterController.enabled)
                return true;

            bool canTryExit = forklift != null && forklift.isForklift &&
                              forklift.hasTransform &&
                              forklift.Transform != null &&
                              forklift.Transform.gameObject.activeInHierarchy &&
                              forklift.hasDriverExitAnchor &&
                              forklift.DriverExitAnchor != null &&
                              forklift.DriverExitAnchor.gameObject
                                  .activeInHierarchy &&
                              driver.hasTransform && driver.Transform != null;
            if (canTryExit)
            {
                if (_motion.TryExitDriver(
                        driver.CharacterController,
                        driver.Transform,
                        forklift.DriverExitAnchor,
                        forklift.Transform))
                {
                    return true;
                }

                if (forklift.isDestructed)
                    // Keep DrivingForklift until the regular Destructed cleanup
                    // releases the blocked vehicle view. The next frame restores
                    // the controller with no forklift collision hull around it.
                    return false;
            }

            if (forklift != null && forklift.isDestructed)
                // Missing anchors are handled by the same one-frame defer.
                return false;

            _motion.RestoreDriverController(driver.CharacterController);
            return true;
        }

        private static void SetParkedForkliftInteraction(GameEntity entity)
        {
            if (!entity.isForklift)
                return;

            entity.isInteractable = !entity.isDestructed;
            entity.isHighlighted = false;
        }

        private static void ResetPlayerState(GameEntity player)
        {
            if (player.hasFocusedEntityId)
                player.RemoveFocusedEntityId();
            if (player.hasFocusedInteractionType)
                player.RemoveFocusedInteractionType();
            if (player.hasInteractionPrompt)
                player.RemoveInteractionPrompt();
            player.isFocusInteractionAvailable = false;
            if (player.hasMoveDirection)
                player.ReplaceMoveDirection(Vector3.zero);
            if (player.hasVerticalVelocity)
                player.ReplaceVerticalVelocity(0f);
            if (player.hasHorizontalSpeed)
                player.ReplaceHorizontalSpeed(0f);
            if (player.hasViewPivot && player.ViewPivot != null)
                player.ViewPivot.localRotation = Quaternion.identity;
            if (player.hasViewPitch)
                player.ReplaceViewPitch(0f);
        }

        private void PrepareLiveBays()
        {
            _occupiedSlotsByBayId.Clear();
            _storeByBayId.Clear();

            foreach (GameEntity bay in _bayBuffer)
            {
                if (bay.isDestructed)
                {
                    bay.isInteractable = false;
                    bay.isHighlighted = false;
                    continue;
                }

                if (!TryGetValidBayStoreEntityId(bay, out int storeEntityId))
                {
                    throw new InvalidOperationException(
                        $"Live pallet bay {bay.EntityId} has invalid configuration.");
                }

                _occupiedSlotsByBayId.Add(
                    bay.EntityId, new bool[bay.Slots.Length]);
                _storeByBayId.Add(bay.EntityId, storeEntityId);
            }
        }

        private static bool TryGetValidBayStoreEntityId(GameEntity bay,
            out int storeEntityId)
        {
            storeEntityId = 0;
            bool truckRole = bay.isFreightTruck &&
                             bay.hasFreightTruckStoreEntityId &&
                             !bay.isFreightStagingZone &&
                             !bay.hasFreightStagingZoneStoreEntityId;
            bool stagingRole = bay.isFreightStagingZone &&
                               bay.hasFreightStagingZoneStoreEntityId &&
                               !bay.isFreightTruck &&
                               !bay.hasFreightTruckStoreEntityId;
            if (truckRole == stagingRole || !bay.hasSlots ||
                bay.Slots == null || bay.Slots.Length == 0 ||
                !bay.hasOccupiedPalletSlotCount ||
                bay.OccupiedPalletSlotCount < 0 ||
                bay.OccupiedPalletSlotCount > bay.Slots.Length)
            {
                return false;
            }

            foreach (Transform slot in bay.Slots)
            {
                if (slot == null || !slot.gameObject.activeInHierarchy)
                    return false;
            }

            storeEntityId = truckRole
                ? bay.FreightTruckStoreEntityId
                : bay.FreightStagingZoneStoreEntityId;
            return storeEntityId > 0;
        }

        private void DetachInvalidRelationOwners()
        {
            foreach (GameEntity relationOwner in
                     _carrierRelations.GetEntities(_relationBuffer))
            {
                if (!relationOwner.isPallet)
                {
                    if (!relationOwner.isDestructed)
                    {
                        throw new InvalidOperationException(
                            "A live non-pallet entity owns ForkliftCarrierEntityId.");
                    }

                    relationOwner.RemoveForkliftCarrierEntityId();
                    continue;
                }
                if (!relationOwner.isDestructed &&
                    !relationOwner.hasEntityId)
                {
                    throw new InvalidOperationException(
                        "A live pallet carrier relation owner has no EntityId.");
                }
                if (!relationOwner.isDestructed)
                {
                    continue;
                }

                relationOwner.RemoveForkliftCarrierEntityId();
            }

            foreach (GameEntity relationOwner in
                     _bayRelations.GetEntities(_relationBuffer))
            {
                if (!relationOwner.isPallet)
                {
                    if (!relationOwner.isDestructed)
                    {
                        throw new InvalidOperationException(
                            "A live non-pallet entity owns PalletBayEntityId.");
                    }

                    DetachPalletBayPlacement(relationOwner);
                    continue;
                }
                if (!relationOwner.isDestructed &&
                    !relationOwner.hasEntityId)
                {
                    throw new InvalidOperationException(
                        "A live pallet bay relation owner has no EntityId.");
                }
                if (!relationOwner.isDestructed)
                {
                    continue;
                }

                DetachPalletBayPlacement(relationOwner);
            }

            foreach (GameEntity pallet in _palletBuffer)
            {
                if (!pallet.isDestructed)
                    continue;

                DetachPalletPlacement(pallet);
            }
        }

        private void ReconcileLivePalletPlacements()
        {
            _recoveryPallets.Clear();
            _queuedPalletIds.Clear();

            foreach (GameEntity pallet in _palletBuffer)
            {
                if (pallet.isDestructed)
                    continue;

                bool hasValidCarrier = HasValidLiveCarrier(pallet);
                if (hasValidCarrier)
                {
                    DetachPalletBayPlacement(pallet);
                    continue;
                }

                bool hasValidBay = TryClaimValidBaySlot(pallet);
                if (hasValidBay)
                {
                    if (pallet.hasForkliftCarrierEntityId)
                        pallet.RemoveForkliftCarrierEntityId();
                    CompleteInboundPalletInStaging(pallet);
                    continue;
                }

                DetachPalletPlacement(pallet);
                QueuePalletForRecovery(pallet);
            }
        }

        private bool HasValidLiveCarrier(GameEntity pallet)
        {
            if (!pallet.hasForkliftCarrierEntityId ||
                !pallet.hasPalletStoreEntityId)
            {
                return false;
            }

            GameEntity forklift = _gameContext.GetEntityWithEntityId(
                pallet.ForkliftCarrierEntityId);
            return forklift != null && forklift.isForklift &&
                   forklift.hasEntityId && !forklift.isDestructed &&
                   forklift.hasForkliftStoreEntityId &&
                   forklift.ForkliftStoreEntityId ==
                   pallet.PalletStoreEntityId &&
                   forklift.hasCargoAnchor && forklift.CargoAnchor != null &&
                   forklift.CargoAnchor.gameObject.activeInHierarchy;
        }

        private bool TryClaimValidBaySlot(GameEntity pallet)
        {
            if (!pallet.hasPalletBayEntityId ||
                !pallet.hasPalletBaySlotIndex ||
                !pallet.hasPalletStoreEntityId ||
                !_occupiedSlotsByBayId.TryGetValue(
                    pallet.PalletBayEntityId, out bool[] occupiedSlots) ||
                !_storeByBayId.TryGetValue(
                    pallet.PalletBayEntityId, out int bayStoreEntityId) ||
                bayStoreEntityId != pallet.PalletStoreEntityId)
            {
                return false;
            }

            int slotIndex = pallet.PalletBaySlotIndex;
            if (slotIndex < 0 || slotIndex >= occupiedSlots.Length ||
                occupiedSlots[slotIndex])
            {
                return false;
            }

            occupiedSlots[slotIndex] = true;
            return true;
        }

        private void CompleteInboundPalletInStaging(GameEntity pallet)
        {
            if (!pallet.isInboundPallet || !pallet.hasPalletBayEntityId)
                return;

            GameEntity bay = _gameContext.GetEntityWithEntityId(
                pallet.PalletBayEntityId);
            if (bay != null && !bay.isDestructed &&
                bay.isFreightStagingZone)
            {
                pallet.isInboundPallet = false;
            }
        }

        private void QueuePalletForRecovery(GameEntity pallet)
        {
            if (_queuedPalletIds.Add(pallet.EntityId))
                _recoveryPallets.Add(pallet);
        }

        private void RecalculateLiveBayCounts()
        {
            foreach (bool[] occupiedSlots in _occupiedSlotsByBayId.Values)
                Array.Clear(occupiedSlots, 0, occupiedSlots.Length);

            foreach (GameEntity pallet in _palletBuffer)
            {
                if (pallet.isDestructed ||
                    !TryClaimValidBaySlotForRecount(pallet))
                {
                    continue;
                }
            }

            foreach (GameEntity bay in _bayBuffer)
            {
                if (bay.isDestructed ||
                    !_occupiedSlotsByBayId.TryGetValue(
                        bay.EntityId, out bool[] occupiedSlots))
                {
                    continue;
                }

                int occupiedCount = 0;
                foreach (bool occupied in occupiedSlots)
                {
                    if (occupied)
                        occupiedCount++;
                }

                bay.ReplaceOccupiedPalletSlotCount(occupiedCount);
            }
        }

        private bool TryClaimValidBaySlotForRecount(GameEntity pallet)
        {
            if (!pallet.hasPalletBayEntityId)
                return false;

            if (pallet.hasForkliftCarrierEntityId ||
                !pallet.hasPalletBaySlotIndex ||
                !pallet.hasPalletStoreEntityId ||
                !_occupiedSlotsByBayId.TryGetValue(
                    pallet.PalletBayEntityId, out bool[] occupiedSlots) ||
                !_storeByBayId.TryGetValue(
                    pallet.PalletBayEntityId, out int bayStoreEntityId) ||
                bayStoreEntityId != pallet.PalletStoreEntityId ||
                pallet.PalletBaySlotIndex < 0 ||
                pallet.PalletBaySlotIndex >= occupiedSlots.Length ||
                occupiedSlots[pallet.PalletBaySlotIndex])
            {
                DestroyUnrecoverablePallet(pallet);
                return false;
            }

            occupiedSlots[pallet.PalletBaySlotIndex] = true;
            return true;
        }

        private void RecoverOrDestroyOrphanedPallets()
        {
            foreach (GameEntity pallet in _recoveryPallets)
            {
                if (pallet.isDestructed)
                    continue;

                if (!TryRecoverPalletToStaging(pallet))
                    DestroyUnrecoverablePallet(pallet);
            }
        }

        private bool TryRecoverPalletToStaging(GameEntity pallet)
        {
            if (!pallet.hasPalletStoreEntityId ||
                !pallet.hasTransform || pallet.Transform == null ||
                !pallet.Transform.gameObject.activeInHierarchy)
            {
                return false;
            }

            GameEntity staging =
                _gameContext.GetEntityWithFreightStagingZoneStoreEntityId(
                    pallet.PalletStoreEntityId);
            if (staging == null || staging.isDestructed ||
                !staging.isPalletBay || !staging.isFreightStagingZone ||
                !staging.hasEntityId ||
                !_occupiedSlotsByBayId.TryGetValue(
                    staging.EntityId, out bool[] occupiedSlots))
            {
                return false;
            }

            int freeSlotIndex = FindFreeSlot(occupiedSlots);
            if (freeSlotIndex < 0)
                return false;

            _motion.PlacePallet(
                pallet.Transform,
                staging.Slots[freeSlotIndex]);

            DetachPalletPlacement(pallet);
            pallet.AddPalletBayEntityId(staging.EntityId);
            pallet.AddPalletBaySlotIndex(freeSlotIndex);
            occupiedSlots[freeSlotIndex] = true;
            pallet.isInboundPallet = false;
            return true;
        }

        private static int FindFreeSlot(bool[] occupiedSlots)
        {
            for (int index = 0; index < occupiedSlots.Length; index++)
            {
                if (!occupiedSlots[index])
                    return index;
            }

            return -1;
        }

        private static void DestroyUnrecoverablePallet(GameEntity entity)
        {
            if (!entity.isPallet)
            {
                if (!entity.isDestructed)
                {
                    throw new InvalidOperationException(
                        "A live non-pallet entity owns a pallet placement relation.");
                }

                DetachPalletPlacement(entity);
                return;
            }

            DetachPalletPlacement(entity);
            entity.isInteractable = false;
            entity.isHighlighted = false;
            entity.isInboundPallet = false;
            entity.isDestructed = true;
        }

        private void FinalizeDestructedEndpoints()
        {
            foreach (GameEntity relationOwner in
                     _carrierRelations.GetEntities(_relationBuffer))
            {
                GameEntity forklift = _gameContext.GetEntityWithEntityId(
                    relationOwner.ForkliftCarrierEntityId);
                if (relationOwner.isPallet && !relationOwner.isDestructed &&
                    forklift != null && forklift.isForklift &&
                    !forklift.isDestructed)
                {
                    continue;
                }

                DestroyUnrecoverablePallet(relationOwner);
            }

            foreach (GameEntity relationOwner in
                     _bayRelations.GetEntities(_relationBuffer))
            {
                GameEntity bay = _gameContext.GetEntityWithEntityId(
                    relationOwner.PalletBayEntityId);
                if (relationOwner.isPallet && !relationOwner.isDestructed &&
                    bay != null && bay.isPalletBay && !bay.isDestructed)
                {
                    continue;
                }

                DestroyUnrecoverablePallet(relationOwner);
            }

            foreach (GameEntity forklift in _forkliftBuffer)
            {
                if (!forklift.isDestructed)
                    continue;

                forklift.isInteractable = false;
                forklift.isHighlighted = false;
                if (!forklift.hasForkliftDriverEntityId)
                    continue;

                GameEntity driver = _gameContext.GetEntityWithEntityId(
                    forklift.ForkliftDriverEntityId);
                if (ReleaseDriver(driver, forklift))
                    forklift.RemoveForkliftDriverEntityId();
            }

            foreach (GameEntity driver in _driverBuffer)
            {
                if (driver.isDestructed)
                    driver.isDrivingForklift = false;
            }
        }

        private static void DetachPalletPlacement(GameEntity entity)
        {
            if (entity.hasForkliftCarrierEntityId)
                entity.RemoveForkliftCarrierEntityId();
            DetachPalletBayPlacement(entity);
        }

        private static void DetachPalletBayPlacement(GameEntity entity)
        {
            if (entity.hasPalletBayEntityId)
                entity.RemovePalletBayEntityId();
            if (entity.hasPalletBaySlotIndex)
                entity.RemovePalletBaySlotIndex();
        }

        private static int CompareByEntityId(GameEntity left,
            GameEntity right) => left.EntityId.CompareTo(right.EntityId);
    }
}
