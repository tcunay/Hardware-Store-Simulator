using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Physics;

namespace HardwareStore.Gameplay.Features.Trolley.Systems
{
    public sealed class FollowWorkerTrolleySystem : IExecuteSystem,
        ITearDownSystem
    {
        private readonly GameContext _gameContext;
        private readonly IWorkerTrolleyHitchService _hitch;
        private readonly IGroup<GameEntity> _trolleys;
        private readonly List<GameEntity> _buffer = new(2);

        public FollowWorkerTrolleySystem(GameContext gameContext,
            IWorkerTrolleyHitchService hitch)
        {
            _gameContext = gameContext;
            _hitch = hitch;
            _trolleys = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WorkerTrolley, GameMatcher.EntityId,
                    GameMatcher.TrolleyPusherEntityId,
                    GameMatcher.TrolleyFollowDistance,
                    GameMatcher.WorkerTrolleyHomePosition,
                    GameMatcher.WorkerTrolleyHomeRotation,
                    GameMatcher.Transform, GameMatcher.Rigidbody,
                    GameMatcher.Colliders)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            _hitch.BeginFrame();
            foreach (GameEntity trolley in _trolleys.GetEntities(_buffer))
            {
                GameEntity worker = _gameContext.GetEntityWithEntityId(
                    trolley.TrolleyPusherEntityId);
                if (worker == null || worker.isDestructed ||
                    !worker.isWarehouseWorker || !worker.hasEntityId ||
                    !worker.hasTransform || !worker.hasNavigationAgent ||
                    !worker.hasRigidbody || !worker.hasColliders ||
                    !worker.hasWarehouseWorkerStatus ||
                    !worker.isHandsOccupied || !worker.isPushingWorkerTrolley ||
                    worker.isCarryingProduct)
                    throw new InvalidOperationException(
                        $"Worker trolley {trolley.EntityId} has invalid pusher.");

                _hitch.Maintain(
                    trolley.Rigidbody, trolley.Colliders,
                    worker.Rigidbody, worker.Colliders,
                    trolley.TrolleyFollowDistance);
            }
            _hitch.EndFrame();
        }

        public void TearDown() => _hitch.DetachAll();
    }
}
