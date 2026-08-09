using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Products.Systems
{
    public sealed class ApplyCarriedProductPlacementSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _products;
        private readonly List<GameEntity> _buffer = new(4);

        public ApplyCarriedProductPlacementSystem(GameContext gameContext) =>
            _products = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.Carried,
                    GameMatcher.ProductPlacementDirty,
                    GameMatcher.View,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody,
                    GameMatcher.Colliders,
                    GameMatcher.HeldRotationOffset,
                    GameMatcher.RigidbodyInterpolationMode,
                    GameMatcher.RigidbodyCollisionDetectionMode)
                .NoneOf(
                    GameMatcher.LooseProduct,
                    GameMatcher.Loaded,
                    GameMatcher.LoadingZoneEntityId,
                    GameMatcher.DeliverySlotIndex,
                    GameMatcher.StorageSlotIndex,
                    GameMatcher.LoadingSlotIndex));

        public void Execute()
        {
            foreach (GameEntity product in _products.GetEntities(_buffer))
            {
                ProductPhysicsUtility.ConfigureCarried(product);
                product.isProductPlacementDirty = false;
            }
        }
    }
}
