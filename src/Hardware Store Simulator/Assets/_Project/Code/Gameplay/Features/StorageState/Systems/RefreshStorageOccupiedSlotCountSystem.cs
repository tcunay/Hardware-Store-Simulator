using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.StorageState.Systems
{
    public sealed class RefreshStorageOccupiedSlotCountSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _storageZones;
        private readonly IGroup<GameEntity> _storedProducts;

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
            {
                if (!storageZonesById.TryGetValue(
                        product.StorageZoneEntityId,
                        out GameEntity storageZone))
                    throw new InvalidOperationException(
                        $"Product {product.EntityId} references missing storage zone " +
                        $"{product.StorageZoneEntityId}.");

                if (product.StorageSlotIndex < 0 ||
                    product.StorageSlotIndex >= storageZone.Slots.Length)
                    throw new InvalidOperationException(
                        $"Product {product.EntityId} has invalid storage slot " +
                        $"{product.StorageSlotIndex}.");

                if (!occupiedSlotsByStorage[storageZone.EntityId].Add(product.StorageSlotIndex))
                    throw new InvalidOperationException(
                        $"Storage slot {product.StorageSlotIndex} in zone " +
                        $"{storageZone.EntityId} is occupied more than once.");
            }

            foreach (GameEntity storageZone in _storageZones)
                storageZone.ReplaceOccupiedStorageSlotCount(
                    occupiedSlotsByStorage[storageZone.EntityId].Count);
        }
    }
}
