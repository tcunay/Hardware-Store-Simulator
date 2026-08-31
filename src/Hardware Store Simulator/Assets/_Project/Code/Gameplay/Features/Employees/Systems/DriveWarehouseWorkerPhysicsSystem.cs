using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Physics;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class DriveWarehouseWorkerPhysicsSystem : IExecuteSystem
    {
        private readonly IWarehouseWorkerPhysicsMotor _motor;
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _workers;
        private readonly List<GameEntity> _buffer = new(4);

        public DriveWarehouseWorkerPhysicsSystem(GameContext gameContext,
            IWarehouseWorkerPhysicsMotor motor)
        {
            _gameContext = gameContext;
            _motor = motor;
            _workers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseWorker,
                    GameMatcher.EntityId,
                    GameMatcher.Rigidbody,
                    GameMatcher.Colliders,
                    GameMatcher.NavigationAgent,
                    GameMatcher.MovementSpeed)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity worker in _workers.GetEntities(_buffer))
            {
                GameEntity trolley = ResolveCoupledTrolley(worker);
                float angularSpeed = ResolveAngularSpeed(worker, trolley);
                _motor.Step(
                    worker.Rigidbody,
                    worker.Colliders,
                    worker.NavigationAgent,
                    trolley?.Rigidbody,
                    trolley?.Colliders,
                    worker.MovementSpeed,
                    worker.NavigationAgent.acceleration,
                    angularSpeed,
                    worker.isTrafficYielding);
            }
        }

        private GameEntity ResolveCoupledTrolley(GameEntity worker)
        {
            if (!worker.isPushingWorkerTrolley)
                return null;

            GameEntity trolley = _gameContext
                .GetEntityWithTrolleyPusherEntityId(worker.EntityId);
            if (trolley == null || trolley.isDestructed ||
                !trolley.isWorkerTrolley || !trolley.hasTrolleyMovementSpeed ||
                !trolley.hasTrolleyFollowDistance ||
                !trolley.hasRigidbody || !trolley.hasColliders ||
                trolley.TrolleyMovementSpeed <= 0f ||
                trolley.TrolleyFollowDistance <= 0f)
            {
                throw new InvalidOperationException(
                    $"Pushing warehouse worker {worker.EntityId} has no valid trolley " +
                    "turning geometry.");
            }

            return trolley;
        }

        private static float ResolveAngularSpeed(GameEntity worker,
            GameEntity trolley)
        {
            float angularSpeed = worker.NavigationAgent.angularSpeed;
            if (trolley == null)
                return angularSpeed;

            float maximumTangentialSpeed = Mathf.Min(
                worker.MovementSpeed, trolley.TrolleyMovementSpeed);
            float trolleyAngularSpeed = Mathf.Rad2Deg *
                maximumTangentialSpeed / trolley.TrolleyFollowDistance;
            return Mathf.Min(angularSpeed, trolleyAngularSpeed);
        }
    }
}
