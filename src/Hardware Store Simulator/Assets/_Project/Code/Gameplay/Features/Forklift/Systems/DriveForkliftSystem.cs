using System;
using Entitas;
using HardwareStore.Gameplay.Common.Physics;
using HardwareStore.Gameplay.Common.Time;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Forklift.Systems
{
    public sealed class DriveForkliftSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IForkliftMotionService _motion;
        private readonly ITimeService _time;
        private readonly IGroup<GameEntity> _forklifts;
        private readonly IGroup<InputEntity> _inputs;

        public DriveForkliftSystem(GameContext gameContext,
            InputContext inputContext, IForkliftMotionService motion,
            ITimeService time)
        {
            _gameContext = gameContext;
            _motion = motion;
            _time = time;
            _forklifts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Forklift,
                    GameMatcher.EntityId,
                    GameMatcher.ForkliftStoreEntityId,
                    GameMatcher.ForkliftDriverEntityId,
                    GameMatcher.ForkliftForwardSpeed,
                    GameMatcher.ForkliftReverseSpeed,
                    GameMatcher.ForkliftSteeringSpeed,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody,
                    GameMatcher.Colliders,
                    GameMatcher.DriverSeatAnchor)
                .NoneOf(GameMatcher.Destructed));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(
                InputMatcher.InputState,
                InputMatcher.MoveInput));
        }

        public void Execute()
        {
            foreach (InputEntity input in _inputs)
            foreach (GameEntity forklift in _forklifts)
            {
                GameEntity driver = ResolveDriver(forklift);
                Vector2 move = Vector2.ClampMagnitude(input.MoveInput, 1f);
                _motion.TryDrive(
                    forklift.Rigidbody,
                    forklift.Colliders,
                    move.y,
                    move.x,
                    forklift.ForkliftForwardSpeed,
                    forklift.ForkliftReverseSpeed,
                    forklift.ForkliftSteeringSpeed,
                    _time.DeltaTime);
                _motion.KeepDriverSeated(
                    driver.CharacterController,
                    driver.Transform,
                    forklift.DriverSeatAnchor);
            }
        }

        private GameEntity ResolveDriver(GameEntity forklift)
        {
            GameEntity driver = _gameContext.GetEntityWithEntityId(
                forklift.ForkliftDriverEntityId);
            if (driver == null || !driver.isPlayer || !driver.hasEntityId ||
                !driver.hasStoreEntityId ||
                driver.StoreEntityId != forklift.ForkliftStoreEntityId ||
                !driver.isDrivingForklift || driver.isHandsOccupied ||
                driver.isModalOpen || driver.isDestructed ||
                !driver.hasTransform || !driver.hasCharacterController)
            {
                throw new InvalidOperationException(
                    $"Forklift {forklift.EntityId} has an invalid driver.");
            }

            return driver;
        }
    }
}
