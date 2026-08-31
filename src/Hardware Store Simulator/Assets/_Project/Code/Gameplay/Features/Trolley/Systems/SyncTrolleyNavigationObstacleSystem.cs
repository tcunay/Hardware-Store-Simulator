using System;
using Entitas;
using UnityEngine.AI;

namespace HardwareStore.Gameplay.Features.Trolley.Systems
{
    public sealed class SyncTrolleyNavigationObstacleSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _trolleys;

        public SyncTrolleyNavigationObstacleSystem(GameContext gameContext)
        {
            _trolleys = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.PlatformTrolley,
                    GameMatcher.EntityId,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody,
                    GameMatcher.NavMeshObstacle)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity trolley in _trolleys)
            {
                NavMeshObstacle obstacle = trolley.NavMeshObstacle;
                if (obstacle.gameObject != trolley.Rigidbody.gameObject ||
                    trolley.Transform != trolley.Rigidbody.transform)
                {
                    throw new InvalidOperationException(
                        $"Platform trolley {trolley.EntityId} has an invalid navigation " +
                        "obstacle binding.");
                }

                bool shouldBeEnabled = !trolley.hasTrolleyPusherEntityId;
                if (obstacle.enabled != shouldBeEnabled)
                    obstacle.enabled = shouldBeEnabled;
            }
        }
    }
}
