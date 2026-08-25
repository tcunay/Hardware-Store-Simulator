using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Features.StorageState.Systems
{
    public sealed class RefreshRequiredProductAvailabilitySystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _consumers;
        private readonly IGroup<GameEntity> _stockedProducts;
        private readonly Dictionary<
            (int StorageZoneEntityId, ProductTypeId ProductType),
            int> _stockByStorageAndType = new(16);

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
            _stockByStorageAndType.Clear();

            foreach (GameEntity product in _stockedProducts)
            {
                var key = (product.StorageZoneEntityId, product.ProductType);
                _stockByStorageAndType.TryGetValue(key, out int count);
                _stockByStorageAndType[key] = checked(count + 1);
            }

            foreach (GameEntity consumer in _consumers)
            {
                _stockByStorageAndType.TryGetValue(
                    (consumer.StorageZoneEntityId, consumer.ProductType),
                    out int availableProductCount);
                consumer.ReplaceAvailableProductCount(availableProductCount);
            }
        }
    }
}
