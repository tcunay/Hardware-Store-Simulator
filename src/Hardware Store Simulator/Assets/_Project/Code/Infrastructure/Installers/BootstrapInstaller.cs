using HardwareStore.Gameplay.Common.Collisions;
using HardwareStore.Gameplay.Common.Cursor;
using HardwareStore.Gameplay.Common.Input;
using HardwareStore.Gameplay.Common.Physics;
using HardwareStore.Gameplay.Common.Time;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.Scene;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using HardwareStore.Infrastructure.Loading;
using HardwareStore.Infrastructure.States.Factory;
using HardwareStore.Infrastructure.States.GameStates;
using HardwareStore.Infrastructure.States.StateMachine;
using HardwareStore.Infrastructure.Systems;
using HardwareStore.Infrastructure.View.Factory;
using Zenject;

namespace HardwareStore.Infrastructure.Installers
{
    public sealed class BootstrapInstaller : MonoInstaller, ICoroutineRunner, IInitializable
    {
        public override void InstallBindings()
        {
            BindInfrastructureServices();
            BindContexts();
            BindGameplayServices();
            BindSystemFactory();
            BindGameplayFactories();
            BindStateMachine();
            BindGameStates();
        }

        public void Initialize() =>
            Container.Resolve<IGameStateMachine>().Enter<BootstrapState>();

        private void BindInfrastructureServices()
        {
            Container.BindInterfacesTo<BootstrapInstaller>().FromInstance(this).AsSingle();
            Container.BindExecutionOrder<BootstrapInstaller>(0);
            Container.Bind<IIdentifierService>().To<IdentifierService>().AsSingle();
            Container.Bind<ITimeService>().To<UnityTimeService>().AsSingle();
            Container.Bind<ICursorService>().To<UnityCursorService>().AsSingle();
            Container.Bind<ICollisionRegistry>().To<CollisionRegistry>().AsSingle();
            Container.Bind<IInteractionPhysicsService>().To<InteractionPhysicsService>().AsSingle();
            Container.Bind<IProductDropPhysicsService>().To<ProductDropPhysicsService>().AsSingle();
            Container.Bind<ITrolleyMotionService>().To<TrolleyMotionService>().AsSingle();
            Container.Bind<ISceneLoader>().To<SceneLoader>().AsSingle();
            Container.Bind<IEntityViewFactory>().To<EntityViewFactory>().AsSingle();
        }

        private void BindContexts()
        {
            Contexts contexts = Contexts.sharedInstance;
            Container.Bind<Contexts>().FromInstance(contexts).AsSingle();
            Container.Bind<GameContext>().FromInstance(contexts.game).AsSingle();
            Container.Bind<InputContext>().FromInstance(contexts.input).AsSingle();
        }

        private void BindGameplayServices()
        {
            Container.Bind<ILocalizationCatalog>().To<RussianLocalizationCatalog>().AsSingle();
            Container.BindInterfacesAndSelfTo<LocalizationService>().AsSingle();
            Container.BindInterfacesAndSelfTo<StaticDataService>().AsSingle();
            Container.BindInterfacesAndSelfTo<InputSystemService>().AsSingle();
            Container.BindInterfacesTo<StoreSceneData>().AsSingle();
        }

        private void BindSystemFactory() =>
            Container.Bind<ISystemFactory>().To<SystemFactory>().AsSingle();

        private void BindGameplayFactories()
        {
            Container.Bind<IPlayerFactory>().To<PlayerFactory>().AsSingle();
            Container.Bind<IStoreFactory>().To<StoreFactory>().AsSingle();
            Container.Bind<ICustomerVisitFactory>().To<CustomerVisitFactory>().AsSingle();
            Container.Bind<ICustomerFactory>().To<CustomerFactory>().AsSingle();
            Container.Bind<IConsultationOfferFactory>().To<ConsultationOfferFactory>().AsSingle();
            Container.Bind<IOrderFactory>().To<OrderFactory>().AsSingle();
            Container.Bind<IProductFactory>().To<ProductFactory>().AsSingle();
            Container.Bind<IDeliveryFactory>().To<DeliveryFactory>().AsSingle();
            Container.Bind<IPlatformTrolleyFactory>().To<PlatformTrolleyFactory>().AsSingle();
            Container.Bind<IInteractionTargetFactory>().To<InteractionTargetFactory>().AsSingle();
            Container.Bind<IGameEventFactory>().To<GameEventFactory>().AsSingle();
        }

        private void BindStateMachine()
        {
            Container.BindInterfacesTo<StateFactory>().AsSingle();
            Container.BindInterfacesTo<GameStateMachine>().AsSingle();
        }

        private void BindGameStates()
        {
            Container.BindInterfacesAndSelfTo<BootstrapState>().AsSingle();
            Container.BindInterfacesAndSelfTo<LoadingStoreState>().AsSingle();
            Container.BindInterfacesAndSelfTo<StoreEnterState>().AsSingle();
            Container.BindInterfacesAndSelfTo<StoreLoopState>().AsSingle();
        }
    }
}
