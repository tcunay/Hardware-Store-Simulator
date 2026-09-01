using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Physics;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class DriveWarehouseWorkerPhysicsSystem : IExecuteSystem
    {
        private readonly IWarehouseWorkerPhysicsMotor _motor;
        private readonly IGroup<GameEntity> _workers;
        private readonly List<GameEntity> _buffer = new(4);

        public DriveWarehouseWorkerPhysicsSystem(GameContext gameContext,
            IWarehouseWorkerPhysicsMotor motor)
        {
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
                _motor.Step(
                    worker.Rigidbody,
                    worker.Colliders,
                    worker.NavigationAgent,
                    worker.MovementSpeed,
                    worker.NavigationAgent.acceleration,
                    worker.NavigationAgent.angularSpeed);
            }
        }
    }
}
