using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.StorageState.Systems
{
    public sealed class RefreshStorageProductCountSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _storageZones;
        private readonly IGroup<GameEntity> _inStockProducts;
        private readonly IGroup<GameEntity> _inStockProductsWithoutStorage;
        private readonly Dictionary<int, StorageZoneState> _storageZonesById = new(4);
        private bool _storageZoneTopologyCaptured;

        public RefreshStorageProductCountSystem(GameContext gameContext)
        {
            _storageZones = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.StorageZone,
                GameMatcher.EntityId,
                GameMatcher.StorageProductCount));
            _inStockProducts = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Product,
                GameMatcher.EntityId,
                GameMatcher.InStock,
                GameMatcher.StorageZoneEntityId));
            _inStockProductsWithoutStorage = gameContext.GetGroup(
                GameMatcher.AllOf(
                        GameMatcher.Product,
                        GameMatcher.EntityId,
                        GameMatcher.InStock)
                    .NoneOf(GameMatcher.StorageZoneEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity product in _inStockProductsWithoutStorage)
                throw new InvalidOperationException(
                    $"In-stock product {product.EntityId} has no storage ownership relation.");

            CaptureOrValidateStorageZoneTopology();
            foreach (StorageZoneState state in _storageZonesById.Values)
                state.ProductCount = 0;

            foreach (GameEntity product in _inStockProducts)
            {
                if (!_storageZonesById.TryGetValue(
                        product.StorageZoneEntityId,
                        out StorageZoneState storageZone))
                {
                    throw new InvalidOperationException(
                        $"In-stock product {product.EntityId} references missing storage zone " +
                        $"{product.StorageZoneEntityId}.");
                }

                storageZone.ProductCount = checked(storageZone.ProductCount + 1);
            }

            foreach (GameEntity storageZone in _storageZones)
                storageZone.ReplaceStorageProductCount(
                    _storageZonesById[storageZone.EntityId].ProductCount);
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
                    !ReferenceEquals(state.Entity, storageZone))
                {
                    throw new InvalidOperationException(
                        $"Storage zone {storageZone.EntityId} changed its runtime topology.");
                }
            }
        }

        private sealed class StorageZoneState
        {
            public readonly GameEntity Entity;
            public int ProductCount;

            public StorageZoneState(GameEntity entity) => Entity = entity;
        }
    }
}
