using System;
using Entitas;

namespace HardwareStore.Gameplay.Features.Products.Systems
{
    public sealed class ValidateProductPlacementSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _products;

        public ValidateProductPlacementSystem(GameContext gameContext) =>
            _products = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Product,
                GameMatcher.EntityId,
                GameMatcher.ProductPlacementDirty,
                GameMatcher.View));

        public void Execute()
        {
            foreach (GameEntity product in _products)
            {
                throw new InvalidOperationException(
                    $"Bound product {product.EntityId} has no complete placement state. " +
                    $"Inbound={product.isInboundProduct}, InStock={product.isInStock}, " +
                    $"Carried={product.hasCarrierEntityId}, Loose={product.isLooseProduct}, " +
                    $"Loaded={product.isLoaded}, " +
                    $"ReservedDelivery={product.hasReservedDeliverySlotIndex}, " +
                    $"ReservedStorage={product.hasReservedStorageSlotIndex}, " +
                    $"ReservedOrderLine={product.hasReservedOrderLineEntityId}, " +
                    $"Trolley={product.hasTrolleyEntityId}.");
            }
        }
    }
}
