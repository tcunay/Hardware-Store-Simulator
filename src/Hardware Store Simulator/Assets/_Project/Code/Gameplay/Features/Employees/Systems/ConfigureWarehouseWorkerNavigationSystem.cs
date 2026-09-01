using System;
using Entitas;
using HardwareStore.Gameplay.Common.Navigation;
using HardwareStore.Gameplay.Common.Physics;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class ConfigureWarehouseWorkerNavigationSystem : IExecuteSystem
    {
        private readonly WarehouseWorkerConfig _config;
        private readonly IWorkerNavigationService _navigation;
        private readonly IGroup<GameEntity> _workers;

        public ConfigureWarehouseWorkerNavigationSystem(GameContext gameContext,
            IStaticDataService staticData, IWorkerNavigationService navigation)
        {
            _config = staticData.WarehouseWorker;
            _navigation = navigation;
            _workers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseWorker,
                    GameMatcher.EntityId,
                    GameMatcher.View,
                    GameMatcher.Transform,
                    GameMatcher.Rigidbody,
                    GameMatcher.Colliders,
                    GameMatcher.NavigationAgent,
                    GameMatcher.CarryAnchor,
                    GameMatcher.WarehouseWorkerPickupPosition,
                    GameMatcher.WarehouseWorkerStoragePosition,
                    GameMatcher.WarehouseWorkerCustomerLoadingPosition,
                    GameMatcher.WarehouseWorkerStatus)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity worker in _workers)
            {
                GhostMoverCollisionProfile.Apply(
                    worker.Rigidbody,
                    worker.Colliders,
                    GhostMoverCollisionProfile.GhostMover);
                _navigation.Configure(worker.NavigationAgent,
                    _config.MovementSpeed,
                    _config.Acceleration,
                    _config.AngularSpeed,
                    _config.StoppingDistance);
                if (!_navigation.TryEnsurePlacedOnNavMesh(worker.NavigationAgent,
                        worker.Transform.position,
                        _config.NavigationSampleRadius))
                {
                    throw new InvalidOperationException(
                        $"Warehouse worker {worker.EntityId} spawn is outside NavMesh.");
                }
            }
        }
    }
}
