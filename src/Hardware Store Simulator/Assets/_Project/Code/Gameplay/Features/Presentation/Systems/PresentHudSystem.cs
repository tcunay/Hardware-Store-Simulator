using System;
using Entitas;
using HardwareStore.Common.Entity;
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
                GameEntity store = _gameContext.GetRequiredEntity(player.StoreEntityId, "player store");
                if (!store.isStore || !store.hasMoney || !store.hasOrderEntityId ||
                    !store.hasProcurementTerminalEntityId || !store.hasStorageZoneEntityId)
                    throw new InvalidOperationException(
                        $"Entity {player.StoreEntityId} is not a configured store.");

                GameEntity order = _gameContext.GetRequiredEntity(store.OrderEntityId, "store order");
                if (!order.isOrder || !order.hasStoreEntityId || order.StoreEntityId != store.EntityId ||
                    !order.hasAvailableProductCount)
                    throw new InvalidOperationException(
                        $"Entity {store.OrderEntityId} is not the configured order of store {store.EntityId}.");

                GameEntity procurementTerminal = _gameContext.GetRequiredEntity(
                    store.ProcurementTerminalEntityId,
                    "store procurement terminal");
                if (!procurementTerminal.isProcurementTerminal ||
                    !procurementTerminal.hasStoreEntityId ||
                    procurementTerminal.StoreEntityId != store.EntityId ||
                    !procurementTerminal.hasStorageZoneEntityId ||
                    procurementTerminal.StorageZoneEntityId != store.StorageZoneEntityId)
                    throw new InvalidOperationException(
                        $"Entity {store.ProcurementTerminalEntityId} is not the configured terminal of store " +
                        $"{store.EntityId}.");

                GameEntity storageZone = _gameContext.GetRequiredEntity(
                    store.StorageZoneEntityId,
                    "store storage zone");
                if (!storageZone.isStorageZone || !storageZone.hasSlots)
                    throw new InvalidOperationException(
                        $"Entity {store.StorageZoneEntityId} is not a configured storage zone.");

                bool hasActiveDelivery = procurementTerminal.hasDeliveryEntityId;
                int deliveryStockedCount = 0;
                int deliveryProductCount = procurementTerminal.DeliveryProductCount;
                if (hasActiveDelivery)
                {
                    GameEntity delivery =
                        _gameContext.GetRequiredEntity(
                            procurementTerminal.DeliveryEntityId,
                            "terminal active delivery");
                    if (!delivery.isDelivery || !delivery.isDeliveryActive)
                        throw new InvalidOperationException("Procurement terminal references an inactive delivery.");

                    deliveryStockedCount = delivery.StockedProductCount;
                    deliveryProductCount = delivery.DeliveryProductCount;
                }

                _hud.Present(new HudSnapshot(
                    ResolveOrderState(order),
                    order.LoadedProductCount,
                    order.RequiredProductCount,
                    store.Money,
                    order.AvailableProductCount,
                    hasActiveDelivery,
                    deliveryStockedCount,
                    deliveryProductCount,
                    player.hasInteractionPrompt ? player.InteractionPrompt : string.Empty,
                    player.hasFocusedEntityId,
                    player.isFocusInteractionAvailable,
                    player.hasHeldProductId,
                    player.isCursorLocked));
            }
        }

        private static HudOrderState ResolveOrderState(GameEntity order)
        {
            if (order.isOrderWaiting)
                return HudOrderState.Waiting;
            if (order.isOrderActive)
                return HudOrderState.Active;
            if (order.isOrderCompleted)
                return HudOrderState.Completed;
            throw new InvalidOperationException("Order entity has no state tag.");
        }
    }
}
