using System;
using HardwareStore.Gameplay.Common.Cursor;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Scene;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Gameplay.Views;
using HardwareStore.Infrastructure.States.StateInfrastructure;
using HardwareStore.Infrastructure.States.StateMachine;

namespace HardwareStore.Infrastructure.States.GameStates
{
    public sealed class StoreEnterState : SimpleState
    {
        private readonly IGameStateMachine _stateMachine;
        private readonly IStoreSceneData _sceneData;
        private readonly IPlayerFactory _playerFactory;
        private readonly IOrderFactory _orderFactory;
        private readonly IProductFactory _productFactory;
        private readonly IInteractionTargetFactory _interactionTargetFactory;
        private readonly ICursorService _cursor;
        private readonly IStaticDataService _staticData;

        public StoreEnterState(IGameStateMachine stateMachine, IStoreSceneData sceneData,
            IPlayerFactory playerFactory, IOrderFactory orderFactory, IProductFactory productFactory,
            IInteractionTargetFactory interactionTargetFactory, ICursorService cursor,
            IStaticDataService staticData)
        {
            _stateMachine = stateMachine;
            _sceneData = sceneData;
            _playerFactory = playerFactory;
            _orderFactory = orderFactory;
            _productFactory = productFactory;
            _interactionTargetFactory = interactionTargetFactory;
            _cursor = cursor;
            _staticData = staticData;
        }

        public override void Enter()
        {
            if (_sceneData.ProductViews.Count < _staticData.Order.RequiredProductCount)
                throw new InvalidOperationException(
                    "The scene must contain enough cement bags to complete the order.");

            _cursor.SetLocked(true);
            GameEntity player = _playerFactory.Create(_sceneData.GetSpawnPoint(SpawnPointId.Player));
            GameEntity wallet = _orderFactory.CreateWallet();
            GameEntity order = _orderFactory.CreateOrder(wallet.EntityId);
            player.AddOrderEntityId(order.EntityId);
            _interactionTargetFactory.CreateOrderCounter(_sceneData.OrderCounterView, order.EntityId);
            GameEntity loadingZone =
                _interactionTargetFactory.CreateLoadingZone(_sceneData.LoadingZoneView, order.EntityId);

            if (loadingZone.LoadingSlots.Length < order.RequiredProductCount)
                throw new InvalidOperationException(
                    "The loading zone must contain enough slots to complete the order.");

            foreach (ProductView productView in _sceneData.ProductViews)
                _productFactory.Create(productView);

            _stateMachine.Enter<StoreLoopState>();
        }
    }
}
