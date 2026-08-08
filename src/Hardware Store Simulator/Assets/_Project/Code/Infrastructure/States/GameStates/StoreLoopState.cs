using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay;
using HardwareStore.Infrastructure.States.StateInfrastructure;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Infrastructure.States.GameStates
{
    public sealed class StoreLoopState : SimpleState, IUpdateable
    {
        private readonly ISystemFactory _systems;
        private readonly Contexts _contexts;
        private StoreFeature _updateFeature;
        private StoreLateFeature _lateFeature;

        public StoreLoopState(ISystemFactory systems, Contexts contexts)
        {
            _systems = systems;
            _contexts = contexts;
        }

        public override void Enter()
        {
            _updateFeature = _systems.Create<StoreFeature>();
            _lateFeature = _systems.Create<StoreLateFeature>();
            _updateFeature.Initialize();
            _lateFeature.Initialize();
        }

        public void Update()
        {
            _updateFeature.Execute();
            _updateFeature.Cleanup();
            _lateFeature.Execute();
            _lateFeature.Cleanup();
        }

        public override void Exit()
        {
            foreach (GameEntity entity in _contexts.game.GetEntities())
                entity.isDestructed = true;

            _lateFeature.Clear();
            _updateFeature.Clear();
            _contexts.Reset();
        }
    }
}
