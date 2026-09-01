using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Physics;

namespace HardwareStore.Gameplay.Features.Trolley.Systems
{
    public sealed class SyncTrolleyCollisionProfileSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _trolleys;
        private readonly List<GameEntity> _buffer = new(4);

        public SyncTrolleyCollisionProfileSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _trolleys = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.EntityId,
                    GameMatcher.Rigidbody,
                    GameMatcher.Colliders)
                .AnyOf(
                    GameMatcher.PlatformTrolley,
                    GameMatcher.WorkerTrolley)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            GhostMoverCollisionProfile.ValidateGhostLayerMatrix();
            foreach (GameEntity trolley in _trolleys.GetEntities(_buffer))
            {
                string profile = HasValidPlayerPusher(trolley)
                    ? GhostMoverCollisionProfile.TrafficObstacle
                    : GhostMoverCollisionProfile.GhostMover;
                GhostMoverCollisionProfile.Apply(
                    trolley.Rigidbody, trolley.Colliders, profile);
            }
        }

        private bool HasValidPlayerPusher(GameEntity trolley)
        {
            if (!trolley.isPlatformTrolley || trolley.isWorkerTrolley ||
                !trolley.hasTrolleyPusherEntityId || trolley.isInteractable)
            {
                return false;
            }

            GameEntity player = _gameContext.GetEntityWithEntityId(
                trolley.TrolleyPusherEntityId);
            return player != null && !player.isDestructed && player.isPlayer &&
                   player.hasEntityId && player.isHandsOccupied &&
                   player.isPushingTrolley && !player.isCarryingProduct;
        }
    }
}
