using Entitas;
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
                GameMatcher.EntityId,
                GameMatcher.HandsOccupied,
                GameMatcher.CarryAnchor));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                GameEntity product = _gameContext.GetEntityWithCarrierEntityId(player.EntityId);
                Transform anchor = player.CarryAnchor;
                Vector3 position = anchor.position;
                Quaternion rotation = anchor.rotation * product.HeldRotationOffset;

                product.Rigidbody.position = position;
                product.Rigidbody.rotation = rotation;
                product.Transform.SetPositionAndRotation(position, rotation);
            }
        }
    }
}
