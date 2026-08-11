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
            var storageZonesById = new Dictionary<int, GameEntity>(_storageZones.count);
            var occupiedSlotsByStorage = new Dictionary<int, HashSet<int>>(_storageZones.count);

            foreach (GameEntity storageZone in _storageZones)
            {
                storageZonesById.Add(storageZone.EntityId, storageZone);
                occupiedSlotsByStorage.Add(storageZone.EntityId, new HashSet<int>());
            }

            foreach (GameEntity product in _storedProducts)
                RegisterSlot(
                    product,
                    product.StorageSlotIndex,
                    storageZonesById,
                    occupiedSlotsByStorage);
            foreach (GameEntity product in _reservedProducts)
                RegisterSlot(
                    product,
                    product.ReservedStorageSlotIndex,
                    storageZonesById,
                    occupiedSlotsByStorage);

            foreach (GameEntity storageZone in _storageZones)
                storageZone.ReplaceOccupiedStorageSlotCount(
                    occupiedSlotsByStorage[storageZone.EntityId].Count);
        }

        private static void RegisterSlot(
            GameEntity product,
            int slotIndex,
            IReadOnlyDictionary<int, GameEntity> storageZonesById,
            IReadOnlyDictionary<int, HashSet<int>> occupiedSlotsByStorage)
        {
            if (product.hasStorageSlotIndex == product.hasReservedStorageSlotIndex)
                throw new InvalidOperationException(
                    $"Product {product.EntityId} must own exactly one storage slot state.");
            if (!storageZonesById.TryGetValue(
                    product.StorageZoneEntityId,
                    out GameEntity storageZone))
                throw new InvalidOperationException(
                    $"Product {product.EntityId} references missing storage zone " +
                    $"{product.StorageZoneEntityId}.");

            if (slotIndex < 0 || slotIndex >= storageZone.Slots.Length)
                throw new InvalidOperationException(
                    $"Product {product.EntityId} has invalid storage slot {slotIndex}.");

            if (!occupiedSlotsByStorage[storageZone.EntityId].Add(slotIndex))
                throw new InvalidOperationException(
                    $"Storage slot {slotIndex} in zone {storageZone.EntityId} is occupied or " +
                    "reserved more than once.");
        }
    }
}
