using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Features.StorageState.Systems
{
    public sealed class RefreshOrderAvailableProductCountSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _orders;
        private readonly IGroup<GameEntity> _stockedProducts;

        public RefreshOrderAvailableProductCountSystem(GameContext gameContext)
        {
            _orders = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Order,
                GameMatcher.StorageZoneEntityId,
                GameMatcher.RequiredProductType));
            _stockedProducts = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Product,
                GameMatcher.InStock,
                GameMatcher.StorageZoneEntityId,
                GameMatcher.ProductType));
        }

        public void Execute()
        {
            var stockByStorageAndType =
                new Dictionary<(int StorageZoneEntityId, ProductTypeId ProductType), int>();

            foreach (GameEntity product in _stockedProducts)
            {
                var key = (product.StorageZoneEntityId, product.ProductType);
                stockByStorageAndType.TryGetValue(key, out int count);
                stockByStorageAndType[key] = count + 1;
            }

            foreach (GameEntity order in _orders)
            {
                stockByStorageAndType.TryGetValue(
                    (order.StorageZoneEntityId, order.RequiredProductType),
                    out int availableProductCount);
                order.ReplaceAvailableProductCount(availableProductCount);
            }
        }
    }
}
