using HardwareStore.Gameplay.Common.Cursor;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Scene;
using HardwareStore.Infrastructure.States.StateInfrastructure;
using HardwareStore.Infrastructure.States.StateMachine;

namespace HardwareStore.Infrastructure.States.GameStates
{
    public sealed class StoreEnterState : SimpleState
    {
        private readonly IGameStateMachine _stateMachine;
        private readonly IStoreSceneData _sceneData;
        private readonly IStoreFactory _storeFactory;
        private readonly IPlayerFactory _playerFactory;
        private readonly IForkliftFactory _forkliftFactory;
        private readonly IFreightFoundationFactory _freightFoundationFactory;
        private readonly ICursorService _cursor;

        public StoreEnterState(IGameStateMachine stateMachine, IStoreSceneData sceneData,
            IStoreFactory storeFactory, IPlayerFactory playerFactory,
            IForkliftFactory forkliftFactory,
            IFreightFoundationFactory freightFoundationFactory,
            ICursorService cursor)
        {
            _stateMachine = stateMachine;
            _sceneData = sceneData;
            _storeFactory = storeFactory;
            _playerFactory = playerFactory;
            _forkliftFactory = forkliftFactory;
            _freightFoundationFactory = freightFoundationFactory;
            _cursor = cursor;
        }

        public override void Enter()
        {
            _cursor.SetLocked(true);
            GameEntity store = _storeFactory.Create(_sceneData);
            _playerFactory.Create(
                _sceneData.GetSpawnPoint(SpawnPointId.Player),
                store.EntityId);
            _forkliftFactory.Create(
                _sceneData.GetSpawnPoint(SpawnPointId.Forklift),
                store.EntityId);
            _freightFoundationFactory.Create(
                _sceneData.GetSpawnPoint(SpawnPointId.FreightTruck),
                _sceneData.GetSpawnPoint(SpawnPointId.InboundPallet),
                store.EntityId);

            _stateMachine.Enter<StoreLoopState>();
        }
    }
}
