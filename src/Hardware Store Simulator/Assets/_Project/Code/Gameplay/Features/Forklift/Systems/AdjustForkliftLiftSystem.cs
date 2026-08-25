using System;
using Entitas;
using HardwareStore.Gameplay.Common.Physics;
using HardwareStore.Gameplay.Common.Time;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Forklift.Systems
{
    public sealed class AdjustForkliftLiftSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IForkliftMotionService _motion;
        private readonly ITimeService _time;
        private readonly IGroup<GameEntity> _forklifts;
        private readonly IGroup<InputEntity> _inputs;

        public AdjustForkliftLiftSystem(GameContext gameContext,
            InputContext inputContext, ITimeService time,
            IForkliftMotionService motion)
        {
            _gameContext = gameContext;
            _time = time;
            _motion = motion;
            _forklifts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Forklift,
                    GameMatcher.EntityId,
                    GameMatcher.ForkliftDriverEntityId,
                    GameMatcher.ForkliftForkHeight,
                    GameMatcher.ForkliftMinForkHeight,
                    GameMatcher.ForkliftMaxForkHeight,
                    GameMatcher.ForkliftLiftSpeed,
                    GameMatcher.Transform,
                    GameMatcher.LiftTransform,
                    GameMatcher.Colliders)
                .NoneOf(GameMatcher.Destructed));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(
                InputMatcher.InputState,
                InputMatcher.ForkliftLiftInput));
        }

        public void Execute()
        {
            foreach (InputEntity input in _inputs)
            foreach (GameEntity forklift in _forklifts)
            {
                ValidateDriver(forklift);
                float liftInput = Mathf.Clamp(input.ForkliftLiftInput, -1f, 1f);
                float height = Mathf.Clamp(
                    forklift.ForkliftForkHeight +
                    liftInput * forklift.ForkliftLiftSpeed * _time.DeltaTime,
                    forklift.ForkliftMinForkHeight,
                    forklift.ForkliftMaxForkHeight);
                if (!Mathf.Approximately(height, forklift.ForkliftForkHeight) &&
                    _motion.TrySetForkHeight(
                        forklift.Transform,
                        forklift.LiftTransform,
                        forklift.Colliders,
                        height))
                {
                    forklift.ReplaceForkliftForkHeight(height);
                }
            }
        }

        private void ValidateDriver(GameEntity forklift)
        {
            GameEntity driver = _gameContext.GetEntityWithEntityId(
                forklift.ForkliftDriverEntityId);
            if (driver == null || !driver.isPlayer ||
                !driver.isDrivingForklift || driver.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Forklift {forklift.EntityId} cannot move its lift without a driver.");
            }
        }
    }
}
