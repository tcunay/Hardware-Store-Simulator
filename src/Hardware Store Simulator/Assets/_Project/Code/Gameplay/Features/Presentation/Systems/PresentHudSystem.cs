using System;
using Entitas;
using HardwareStore.Gameplay.Presentation;

namespace HardwareStore.Gameplay.Features.Presentation.Systems
{
    public sealed class PresentHudSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IHudService _hud;
        private readonly IGroup<GameEntity> _players;

        public PresentHudSystem(GameContext gameContext, IHudService hud)
        {
            _gameContext = gameContext;
            _hud = hud;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.EntityId,
                GameMatcher.StoreEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                GameEntity store =
                    _gameContext.GetEntityWithEntityId(player.StoreEntityId);
                GameEntity procurementTerminal = _gameContext.GetEntityWithEntityId(
                    store.ProcurementTerminalEntityId);
                GameEntity storageZone = _gameContext.GetEntityWithEntityId(
                    store.StorageZoneEntityId);
                GameEntity delivery =
                    _gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                        procurementTerminal.EntityId);

                bool hasActiveDelivery = delivery != null;
                int deliveryStockedCount = 0;
                int deliveryProductCount = procurementTerminal.DeliveryProductCount;
                if (hasActiveDelivery)
                {
                    deliveryStockedCount = delivery.StockedProductCount;
                    deliveryProductCount = delivery.DeliveryProductCount;
                }

                HudOrderState orderState;
                int loadedProductCount = 0;
                int requiredProductCount = 0;
                GameEntity customerVisit =
                    _gameContext.GetEntityWithCustomerVisitStoreEntityId(store.EntityId);
                if (customerVisit != null)
                {
                    orderState = ResolveOrderState(customerVisit);
                    loadedProductCount = customerVisit.LoadedProductCount;
                    requiredProductCount = customerVisit.RequiredProductCount;
                }
                else
                {
                    orderState = HudOrderState.NoCustomer;
                }

                _hud.Present(new HudSnapshot(
                    orderState,
                    loadedProductCount,
                    requiredProductCount,
                    store.Money,
                    storageZone.StorageProductCount,
                    hasActiveDelivery,
                    deliveryStockedCount,
                    deliveryProductCount,
                    player.hasInteractionPrompt ? player.InteractionPrompt : string.Empty,
                    player.hasFocusedEntityId,
                    player.isFocusInteractionAvailable,
                    player.isHandsOccupied,
                    player.isCursorLocked));
            }
        }

        private static HudOrderState ResolveOrderState(GameEntity customerVisit)
        {
            ValidateSingleLifecycleState(customerVisit);
            if (customerVisit.isCustomerVisitArriving)
                return HudOrderState.Arriving;
            if (customerVisit.isCustomerVisitDeparting)
                return HudOrderState.Departing;
            if (customerVisit.isCustomerVisitWaiting)
                return HudOrderState.Waiting;
            if (customerVisit.isCustomerVisitLoading)
                return HudOrderState.Active;
            if (customerVisit.isCustomerVisitCompleted)
                return HudOrderState.Completed;

            throw new InvalidOperationException(
                $"Customer visit {customerVisit.EntityId} has no lifecycle state.");
        }

        private static void ValidateSingleLifecycleState(GameEntity customerVisit)
        {
            int lifecycleStateCount =
                (customerVisit.isCustomerVisitArriving ? 1 : 0) +
                (customerVisit.isCustomerVisitWaiting ? 1 : 0) +
                (customerVisit.isCustomerVisitLoading ? 1 : 0) +
                (customerVisit.isCustomerVisitCompleted ? 1 : 0) +
                (customerVisit.isCustomerVisitDeparting ? 1 : 0);
            if (lifecycleStateCount != 1)
                throw new InvalidOperationException(
                    $"Customer visit {customerVisit.EntityId} must have exactly one " +
                    "lifecycle state.");
        }
    }
}
