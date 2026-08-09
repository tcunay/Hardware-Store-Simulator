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

            var productsByStorage = new Dictionary<int, int>(_storageZones.count);
            foreach (GameEntity storageZone in _storageZones)
                productsByStorage.Add(storageZone.EntityId, 0);

            foreach (GameEntity product in _inStockProducts)
            {
                if (!productsByStorage.TryGetValue(
                        product.StorageZoneEntityId,
                        out int productCount))
                {
                    throw new InvalidOperationException(
                        $"In-stock product {product.EntityId} references missing storage zone " +
                        $"{product.StorageZoneEntityId}.");
                }

                productsByStorage[product.StorageZoneEntityId] = productCount + 1;
            }

            foreach (GameEntity storageZone in _storageZones)
                storageZone.ReplaceStorageProductCount(
                    productsByStorage[storageZone.EntityId]);
        }
    }
}
