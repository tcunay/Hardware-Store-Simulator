using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay;
using HardwareStore.Gameplay.Common.VehicleTraffic;
using HardwareStore.Infrastructure.States.StateInfrastructure;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Infrastructure.States.GameStates
{
    public sealed class StoreLoopState : SimpleState, IUpdateable,
        IFixedUpdateable
    {
        private readonly ISystemFactory _systems;
        private readonly Contexts _contexts;
        private readonly IVehicleTrafficService _vehicleTraffic;
        private StoreFeature _updateFeature;
        private StoreFixedFeature _fixedFeature;
        private StoreLateFeature _lateFeature;

        public StoreLoopState(ISystemFactory systems, Contexts contexts,
            IVehicleTrafficService vehicleTraffic)
        {
            _systems = systems;
            _contexts = contexts;
            _vehicleTraffic = vehicleTraffic;
        }

        public override void Enter()
        {
            _updateFeature = _systems.Create<StoreFeature>();
            _fixedFeature = _systems.Create<StoreFixedFeature>();
            _lateFeature = _systems.Create<StoreLateFeature>();
            _updateFeature.Initialize();
            _fixedFeature.Initialize();
            _lateFeature.Initialize();
        }

        public void Update()
        {
            _updateFeature.Execute();
            _updateFeature.Cleanup();
            _lateFeature.Execute();
            _lateFeature.Cleanup();
        }

        public void FixedUpdate()
        {
            _fixedFeature.Execute();
            _fixedFeature.Cleanup();
        }

        public override void Exit()
        {
            foreach (GameEntity entity in _contexts.game.GetEntities())
                entity.isDestructed = true;

            _lateFeature.Clear();
            _fixedFeature.Clear();
            _updateFeature.Clear();
            _vehicleTraffic.Shutdown();
            _contexts.Reset();
        }
    }
}
