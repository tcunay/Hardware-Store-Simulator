using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Loading;
using HardwareStore.Infrastructure.States.StateInfrastructure;
using HardwareStore.Infrastructure.States.StateMachine;

namespace HardwareStore.Infrastructure.States.GameStates
{
    public sealed class BootstrapState : SimpleState
    {
        private readonly ILocalizationService _localization;
        private readonly IStaticDataService _staticData;
        private readonly IGameStateMachine _stateMachine;

        public BootstrapState(ILocalizationService localization, IStaticDataService staticData,
            IGameStateMachine stateMachine)
        {
            _localization = localization;
            _staticData = staticData;
            _stateMachine = stateMachine;
        }

        public override void Enter()
        {
            _localization.Load(LanguageId.Russian);
            _staticData.LoadAll();
            _stateMachine.Enter<LoadingStoreState, string>(Scenes.PrototypeYard);
        }
    }
}
