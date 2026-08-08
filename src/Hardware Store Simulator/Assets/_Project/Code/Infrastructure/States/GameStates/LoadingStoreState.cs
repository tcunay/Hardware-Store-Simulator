using HardwareStore.Infrastructure.Loading;
using HardwareStore.Infrastructure.States.StateInfrastructure;
using HardwareStore.Infrastructure.States.StateMachine;

namespace HardwareStore.Infrastructure.States.GameStates
{
    public sealed class LoadingStoreState : SimplePayloadState<string>
    {
        private readonly ISceneLoader _sceneLoader;
        private readonly IGameStateMachine _stateMachine;

        public LoadingStoreState(ISceneLoader sceneLoader, IGameStateMachine stateMachine)
        {
            _sceneLoader = sceneLoader;
            _stateMachine = stateMachine;
        }

        public override void Enter(string sceneName) =>
            _sceneLoader.LoadScene(sceneName, () => _stateMachine.Enter<StoreEnterState>());
    }
}
