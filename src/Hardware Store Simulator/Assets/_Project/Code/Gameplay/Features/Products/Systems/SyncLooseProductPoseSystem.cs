using Entitas;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Products.Systems
{
    public sealed class SyncLooseProductPoseSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _products;

        public SyncLooseProductPoseSystem(GameContext gameContext) =>
            _products = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.LooseProduct,
                    GameMatcher.Rigidbody,
                    GameMatcher.WorldPosition,
                    GameMatcher.WorldRotation)
                .NoneOf(
                    GameMatcher.CarrierEntityId,
                    GameMatcher.Loaded,
                    GameMatcher.CustomerVisitEntityId,
                    GameMatcher.DeliverySlotIndex,
                    GameMatcher.StorageSlotIndex,
                    GameMatcher.LoadingSlotIndex,
                    GameMatcher.ProductPlacementDirty));

        public void Execute()
        {
            foreach (GameEntity product in _products)
            {
                Vector3 position = product.Rigidbody.position;
                Quaternion rotation = product.Rigidbody.rotation;
                if (product.WorldPosition != position)
                    product.ReplaceWorldPosition(position);
                if (product.WorldRotation != rotation)
                    product.ReplaceWorldRotation(rotation);
            }
        }
    }
}
