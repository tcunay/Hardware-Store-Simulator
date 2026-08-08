using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Loading;
using HardwareStore.Infrastructure.States.StateInfrastructure;
using HardwareStore.Infrastructure.States.StateMachine;

namespace HardwareStore.Infrastructure.States.GameStates
{
    public sealed class BootstrapState : SimpleState
    {
        private readonly IStaticDataService _staticData;
        private readonly IGameStateMachine _stateMachine;

        public BootstrapState(IStaticDataService staticData, IGameStateMachine stateMachine)
        {
            _staticData = staticData;
            _stateMachine = stateMachine;
        }

        public override void Enter()
        {
            _staticData.LoadAll();
            _stateMachine.Enter<LoadingStoreState, string>(Scenes.PrototypeYard);
        }
    }
}
