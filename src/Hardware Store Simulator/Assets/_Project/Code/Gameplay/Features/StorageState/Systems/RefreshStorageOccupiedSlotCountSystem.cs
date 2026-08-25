using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.StorageState.Systems
{
    public sealed class RefreshStorageOccupiedSlotCountSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _storageZones;
        private readonly IGroup<GameEntity> _storedProducts;
        private readonly IGroup<GameEntity> _reservedProducts;
        private readonly Dictionary<int, StorageZoneState> _storageZonesById = new(4);
        private bool _storageZoneTopologyCaptured;

        public RefreshStorageOccupiedSlotCountSystem(GameContext gameContext)
        {
            _storageZones = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.StorageZone,
                GameMatcher.EntityId,
                GameMatcher.Slots));
            _storedProducts = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Product,
                GameMatcher.EntityId,
                GameMatcher.InStock,
                GameMatcher.StorageZoneEntityId,
                GameMatcher.StorageSlotIndex));
            _reservedProducts = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Product,
                GameMatcher.EntityId,
                GameMatcher.InStock,
                GameMatcher.StorageZoneEntityId,
                GameMatcher.ReservedStorageSlotIndex,
                GameMatcher.ReservedOrderLineEntityId));
        }

        public void Execute()
        {
            CaptureOrValidateStorageZoneTopology();
            foreach (StorageZoneState state in _storageZonesById.Values)
                state.OccupiedSlots.Clear();

            foreach (GameEntity product in _storedProducts)
                RegisterSlot(
                    product,
                    product.StorageSlotIndex,
                    _storageZonesById);
            foreach (GameEntity product in _reservedProducts)
                RegisterSlot(
                    product,
                    product.ReservedStorageSlotIndex,
                    _storageZonesById);

            foreach (GameEntity storageZone in _storageZones)
                storageZone.ReplaceOccupiedStorageSlotCount(
                    _storageZonesById[storageZone.EntityId].OccupiedSlots.Count);
        }

        private void CaptureOrValidateStorageZoneTopology()
        {
            if (!_storageZoneTopologyCaptured)
            {
                foreach (GameEntity storageZone in _storageZones)
                {
                    if (!_storageZonesById.TryAdd(
                            storageZone.EntityId,
                            new StorageZoneState(storageZone)))
                    {
                        throw new InvalidOperationException(
                            $"Storage zone entity id {storageZone.EntityId} is not unique.");
                    }
                }

                _storageZoneTopologyCaptured = true;
                return;
            }

            if (_storageZones.count != _storageZonesById.Count)
            {
                throw new InvalidOperationException(
                    "Storage zone topology cannot change after storage-state refresh starts.");
            }

            foreach (GameEntity storageZone in _storageZones)
            {
                if (!_storageZonesById.TryGetValue(
                        storageZone.EntityId,
                        out StorageZoneState state) ||
                    !ReferenceEquals(state.Entity, storageZone) ||
                    state.SlotCount != storageZone.Slots.Length)
                {
                    throw new InvalidOperationException(
                        $"Storage zone {storageZone.EntityId} changed its runtime topology.");
                }
            }
        }

        private static void RegisterSlot(
            GameEntity product,
            int slotIndex,
            IReadOnlyDictionary<int, StorageZoneState> storageZonesById)
        {
            if (product.hasStorageSlotIndex == product.hasReservedStorageSlotIndex)
                throw new InvalidOperationException(
                    $"Product {product.EntityId} must own exactly one storage slot state.");
            if (!storageZonesById.TryGetValue(
                    product.StorageZoneEntityId,
                    out StorageZoneState storageZone))
                throw new InvalidOperationException(
                    $"Product {product.EntityId} references missing storage zone " +
                    $"{product.StorageZoneEntityId}.");

            if (slotIndex < 0 || slotIndex >= storageZone.SlotCount)
                throw new InvalidOperationException(
                    $"Product {product.EntityId} has invalid storage slot {slotIndex}.");

            if (!storageZone.OccupiedSlots.Add(slotIndex))
                throw new InvalidOperationException(
                    $"Storage slot {slotIndex} in zone " +
                    $"{storageZone.Entity.EntityId} is occupied or reserved more than once.");
        }

        private sealed class StorageZoneState
        {
            public readonly GameEntity Entity;
            public readonly int SlotCount;
            public readonly HashSet<int> OccupiedSlots;

            public StorageZoneState(GameEntity entity)
            {
                Entity = entity;
                SlotCount = entity.Slots.Length;
                OccupiedSlots = new HashSet<int>(SlotCount);
            }
        }
    }
}
