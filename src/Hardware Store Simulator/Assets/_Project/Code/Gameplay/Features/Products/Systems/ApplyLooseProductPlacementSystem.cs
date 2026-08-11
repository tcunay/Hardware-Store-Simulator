using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Products.Systems
{
    public sealed class ApplyLooseProductPlacementSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _products;
        private readonly List<GameEntity> _buffer = new(32);

        public ApplyLooseProductPlacementSystem(GameContext gameContext) =>
            _products = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.LooseProduct,
                    GameMatcher.WorldPosition,
                    GameMatcher.WorldRotation,
                    GameMatcher.ProductPlacementDirty,
                    GameMatcher.View,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody,
                    GameMatcher.Colliders,
                    GameMatcher.RigidbodyInterpolationMode,
                    GameMatcher.RigidbodyCollisionDetectionMode)
                .AnyOf(
                    GameMatcher.ReservedDeliverySlotIndex,
                    GameMatcher.ReservedStorageSlotIndex)
                .NoneOf(
                    GameMatcher.CarrierEntityId,
                    GameMatcher.Loaded,
                    GameMatcher.OrderLineEntityId,
                    GameMatcher.DeliverySlotIndex,
                    GameMatcher.StorageSlotIndex,
                    GameMatcher.LoadingSlotIndex,
                    GameMatcher.TrolleyEntityId,
                    GameMatcher.TrolleySlotIndex));

        public void Execute()
        {
            foreach (GameEntity product in _products.GetEntities(_buffer))
            {
                ProductPhysicsUtility.ConfigureLoose(product);
                product.isProductPlacementDirty = false;
            }
        }
    }
}
