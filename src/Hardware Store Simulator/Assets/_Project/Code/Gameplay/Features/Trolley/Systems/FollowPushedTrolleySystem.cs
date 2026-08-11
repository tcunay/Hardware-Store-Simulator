using System;
using Entitas;
using HardwareStore.Gameplay.Common.Physics;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Trolley.Systems
{
    public sealed class FollowPushedTrolleySystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly ITrolleyMotionService _motion;
        private readonly IGroup<GameEntity> _trolleys;

        public FollowPushedTrolleySystem(GameContext gameContext,
            ITrolleyMotionService motion)
        {
            _gameContext = gameContext;
            _motion = motion;
            _trolleys = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.PlatformTrolley,
                    GameMatcher.EntityId,
                    GameMatcher.TrolleyPusherEntityId,
                    GameMatcher.TrolleyFollowDistance,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody,
                    GameMatcher.Colliders)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity trolley in _trolleys)
            {
                GameEntity player = _gameContext.GetEntityWithEntityId(
                    trolley.TrolleyPusherEntityId);
                if (player == null || !player.isPlayer || !player.hasTransform ||
                    !player.hasCharacterController ||
                    !player.isHandsOccupied || !player.isPushingTrolley ||
                    player.isCarryingProduct || !trolley.Rigidbody.isKinematic)
                {
                    throw new InvalidOperationException(
                        $"Trolley {trolley.EntityId} cannot follow its configured pusher.");
                }

                Transform playerTransform = player.Transform;
                Vector3 position = playerTransform.position +
                                   playerTransform.forward * trolley.TrolleyFollowDistance;
                Quaternion rotation = playerTransform.rotation;
                if (!_motion.CanMoveTo(
                        trolley.Rigidbody,
                        trolley.Colliders,
                        player.CharacterController,
                        position,
                        rotation))
                {
                    continue;
                }

                trolley.Rigidbody.position = position;
                trolley.Rigidbody.rotation = rotation;
                trolley.Transform.SetPositionAndRotation(position, rotation);
            }
        }
    }
}
