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
                    GameMatcher.CarrierEntityId,
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
                    GameMatcher.CustomerVisitEntityId,
                    GameMatcher.DeliverySlotIndex,
                    GameMatcher.StorageSlotIndex,
                    GameMatcher.LoadingSlotIndex,
                    GameMatcher.WorldPosition,
                    GameMatcher.WorldRotation));

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
