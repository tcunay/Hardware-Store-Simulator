using Entitas;
using HardwareStore.Gameplay.Common.Physics;

namespace HardwareStore.Gameplay.Features.Forklift.Systems
{
    public sealed class SyncForkliftLiftTransformSystem : IExecuteSystem
    {
        private readonly IForkliftMotionService _motion;
        private readonly IGroup<GameEntity> _forklifts;

        public SyncForkliftLiftTransformSystem(GameContext gameContext,
            IForkliftMotionService motion)
        {
            _motion = motion;
            _forklifts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Forklift,
                    GameMatcher.ForkliftForkHeight,
                    GameMatcher.Transform,
                    GameMatcher.LiftTransform,
                    GameMatcher.Colliders)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity forklift in _forklifts)
            {
                if (!_motion.TrySetForkHeight(
                        forklift.Transform,
                        forklift.LiftTransform,
                        forklift.Colliders,
                        forklift.ForkliftForkHeight))
                {
                    throw new System.InvalidOperationException(
                        "Forklift lift component requests a blocked view pose.");
                }
            }
        }
    }
}
