using System;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Presentation;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Presentation.Systems
{
    public sealed class PresentHudSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IStaticDataService _staticData;
        private readonly IHudService _hud;
        private readonly IGroup<GameEntity> _players;

        public PresentHudSystem(GameContext gameContext, IStaticDataService staticData,
            IHudService hud)
        {
            _gameContext = gameContext;
            _staticData = staticData;
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
                ProductTypeId deliveryProductType = hasActiveDelivery
                    ? delivery.ProductType
                    : procurementTerminal.SelectedProductType;
                ProductConfig deliveryProduct =
                    _staticData.GetProduct(deliveryProductType);
                int deliveryStockedCount = 0;
                int deliveryProductCount = hasActiveDelivery
                    ? delivery.DeliveryProductCount
                    : _staticData.GetDelivery(deliveryProductType).ProductCount;
                if (hasActiveDelivery)
                    deliveryStockedCount = delivery.StockedProductCount;

                HudOrderState orderState;
                ProductTypeId requiredProductType = deliveryProductType;
                ProductConfig requiredProduct = deliveryProduct;
                int availableProductCount = 0;
                int loadedProductCount = 0;
                int requiredProductCount = 0;
                GameEntity customerVisit =
                    _gameContext.GetEntityWithCustomerVisitStoreEntityId(store.EntityId);
                if (customerVisit != null)
                {
                    orderState = ResolveOrderState(customerVisit);
                    bool hasOrder = customerVisit.isOrder;
                    bool requiresOrder = orderState is not (
                        HudOrderState.Arriving or HudOrderState.Consulting);
                    if (hasOrder != requiresOrder)
                    {
                        throw new InvalidOperationException(
                            $"Customer visit {customerVisit.EntityId} has an order that does " +
                            "not match its lifecycle state.");
                    }
                    if (!hasOrder && !customerVisit.hasRequestedProductType)
                    {
                        throw new InvalidOperationException(
                            $"Customer visit {customerVisit.EntityId} has no requested product.");
                    }

                    requiredProductType = hasOrder
                        ? customerVisit.RequiredProductType
                        : customerVisit.RequestedProductType;
                    requiredProduct = _staticData.GetProduct(requiredProductType);
                    if (hasOrder)
                    {
                        availableProductCount = customerVisit.AvailableProductCount;
                        loadedProductCount = customerVisit.LoadedProductCount;
                        requiredProductCount = customerVisit.RequiredProductCount;
                    }
                }
                else
                {
                    orderState = HudOrderState.NoCustomer;
                }

                string carriedProductDisplayName = string.Empty;
                if (player.isHandsOccupied)
                {
                    GameEntity carriedProduct =
                        _gameContext.GetEntityWithCarrierEntityId(player.EntityId);
                    carriedProductDisplayName =
                        _staticData.GetProduct(carriedProduct.ProductType).DisplayName;
                }

                _hud.Present(new HudSnapshot(
                    orderState,
                    requiredProductType,
                    requiredProduct.DisplayName,
                    requiredProduct.UnitLabel,
                    availableProductCount,
                    loadedProductCount,
                    requiredProductCount,
                    store.Money,
                    storageZone.StorageProductCount,
                    hasActiveDelivery,
                    deliveryProductType,
                    deliveryProduct.DisplayName,
                    deliveryProduct.UnitLabel,
                    deliveryStockedCount,
                    deliveryProductCount,
                    carriedProductDisplayName,
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
            if (customerVisit.isCustomerVisitConsulting)
                return HudOrderState.Consulting;
            if (customerVisit.isCustomerVisitReturning)
                return HudOrderState.Returning;
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
                (customerVisit.isCustomerVisitConsulting ? 1 : 0) +
                (customerVisit.isCustomerVisitWaiting ? 1 : 0) +
                (customerVisit.isCustomerVisitLoading ? 1 : 0) +
                (customerVisit.isCustomerVisitCompleted ? 1 : 0) +
                (customerVisit.isCustomerVisitReturning ? 1 : 0) +
                (customerVisit.isCustomerVisitDeparting ? 1 : 0);
            if (lifecycleStateCount != 1)
                throw new InvalidOperationException(
                    $"Customer visit {customerVisit.EntityId} must have exactly one " +
                    "lifecycle state.");
        }
    }
}
