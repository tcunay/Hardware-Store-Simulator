using System;
using Entitas;
using HardwareStore.Common.Entity;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Carrying.Systems
{
    public sealed class FollowHeldProductSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;

        public FollowHeldProductSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.HeldProductId,
                GameMatcher.CarryAnchor));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                GameEntity product = GetRequiredHeldProduct(player.HeldProductId);
                Transform anchor = player.CarryAnchor;
                Vector3 position = anchor.position;
                Quaternion rotation = anchor.rotation * product.HeldRotationOffset;

                product.Rigidbody.position = position;
                product.Rigidbody.rotation = rotation;
                product.Transform.SetPositionAndRotation(position, rotation);
            }
        }

        private GameEntity GetRequiredHeldProduct(int productEntityId)
        {
            GameEntity product = _gameContext.GetRequiredEntity(
                productEntityId,
                "player held product");
            if (!product.isProduct || !product.isCarried || !product.hasRigidbody ||
                !product.hasTransform || !product.hasHeldRotationOffset)
            {
                throw new InvalidOperationException(
                    $"Entity {productEntityId} is not a bound carried product.");
            }

            return product;
        }
    }
}
