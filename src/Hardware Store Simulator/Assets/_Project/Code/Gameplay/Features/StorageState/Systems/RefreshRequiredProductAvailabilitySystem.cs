using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Features.StorageState.Systems
{
    public sealed class RefreshRequiredProductAvailabilitySystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _consumers;
        private readonly IGroup<GameEntity> _stockedProducts;

        public RefreshRequiredProductAvailabilitySystem(GameContext gameContext)
        {
            _consumers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.ProductType,
                    GameMatcher.AvailableProductCount)
                .AnyOf(
                    GameMatcher.OrderLine,
                    GameMatcher.ConsultationOfferLine)
                .NoneOf(GameMatcher.Destructed));
            _stockedProducts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.InStock,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.ProductType)
                .NoneOf(GameMatcher.Destructed));
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

            foreach (GameEntity consumer in _consumers)
            {
                stockByStorageAndType.TryGetValue(
                    (consumer.StorageZoneEntityId, consumer.ProductType),
                    out int availableProductCount);
                consumer.ReplaceAvailableProductCount(availableProductCount);
            }
        }
    }
}
