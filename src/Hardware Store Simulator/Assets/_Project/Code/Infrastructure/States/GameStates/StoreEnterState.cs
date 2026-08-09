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
        private readonly ICursorService _cursor;

        public StoreEnterState(IGameStateMachine stateMachine, IStoreSceneData sceneData,
            IStoreFactory storeFactory, IPlayerFactory playerFactory, ICursorService cursor)
        {
            _stateMachine = stateMachine;
            _sceneData = sceneData;
            _storeFactory = storeFactory;
            _playerFactory = playerFactory;
            _cursor = cursor;
        }

        public override void Enter()
        {
            _cursor.SetLocked(true);
            GameEntity store = _storeFactory.Create(_sceneData);
            _playerFactory.Create(
                _sceneData.GetSpawnPoint(SpawnPointId.Player),
                store.EntityId);

            _stateMachine.Enter<StoreLoopState>();
        }
    }
}
