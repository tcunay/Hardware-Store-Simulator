using System;
using Entitas;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Carrying.Systems
{
    public sealed class FollowWorkerCarriedProductSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _workers;

        public FollowWorkerCarriedProductSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _workers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseWorker,
                    GameMatcher.EntityId,
                    GameMatcher.HandsOccupied,
                    GameMatcher.CarryingProduct,
                    GameMatcher.CarryAnchor)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity worker in _workers)
            {
                GameEntity product = _gameContext.GetEntityWithCarrierEntityId(
                    worker.EntityId);
                if (product == null || !product.isProduct || product.isDestructed ||
                    !product.hasRigidbody || !product.hasTransform ||
                    !product.hasHeldRotationOffset)
                    throw new InvalidOperationException(
                        $"Warehouse worker {worker.EntityId} has invalid carried product.");

                Transform anchor = worker.CarryAnchor;
                Vector3 position = anchor.position;
                Quaternion rotation = anchor.rotation * product.HeldRotationOffset;
                product.Rigidbody.position = position;
                product.Rigidbody.rotation = rotation;
                product.Transform.SetPositionAndRotation(position, rotation);
            }
        }
    }
}
