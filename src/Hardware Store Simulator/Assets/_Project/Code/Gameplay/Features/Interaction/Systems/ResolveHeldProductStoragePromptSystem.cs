using System;
using Entitas;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveHeldProductStoragePromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;

        public ResolveHeldProductStoragePromptSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.EntityId,
                GameMatcher.StoreEntityId,
                GameMatcher.HandsOccupied,
                GameMatcher.FocusedEntityId,
                GameMatcher.FocusedInteractionType));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                if (player.FocusedInteractionType != InteractionTypeId.StorageZone)
                    continue;

                GameEntity storageZone =
                    _gameContext.GetEntityWithEntityId(player.FocusedEntityId);
                GameEntity store =
                    _gameContext.GetEntityWithEntityId(player.StoreEntityId);
                if (storageZone.EntityId != store.StorageZoneEntityId)
                    continue;

                GameEntity heldProduct =
                    _gameContext.GetEntityWithCarrierEntityId(player.EntityId);

                if (heldProduct.isInStock)
                {
                    if (heldProduct.StorageZoneEntityId != storageZone.EntityId)
                        throw new InvalidOperationException(
                            $"Held stock product {heldProduct.EntityId} does not belong to " +
                            $"storage zone {storageZone.EntityId}.");

                    ResolveHeldStockPrompt(player, store, heldProduct);
                    continue;
                }

                if (!heldProduct.isInboundProduct)
                {
                    player.SetInteractionPrompt(
                        "На приёмку можно положить только товар из поставки",
                        false);
                    continue;
                }

                GameEntity terminal = _gameContext.GetEntityWithEntityId(
                    store.ProcurementTerminalEntityId);
                GameEntity delivery =
                    _gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                        terminal.EntityId);
                if (delivery == null ||
                    delivery.EntityId != heldProduct.DeliveryEntityId)
                    throw new InvalidOperationException(
                        $"Inbound product {heldProduct.EntityId} does not belong to the active " +
                        $"delivery of procurement terminal {terminal.EntityId}.");

                if (storageZone.OccupiedStorageSlotCount >= storageZone.Slots.Length)
                {
                    player.SetInteractionPrompt(
                        "На складе нет свободного места",
                        false);
                    continue;
                }

                player.SetInteractionPrompt("E — принять мешок на склад", true);
            }
        }

        private void ResolveHeldStockPrompt(
            GameEntity player,
            GameEntity store,
            GameEntity heldProduct)
        {
            GameEntity customerVisit =
                _gameContext.GetEntityWithCustomerVisitStoreEntityId(store.EntityId);
            if (customerVisit == null)
            {
                player.SetInteractionPrompt(
                    "Нет активного заказа — положите мешок клавишей G",
                    false);
                return;
            }

            if (customerVisit.isCustomerVisitArriving)
            {
                player.SetInteractionPrompt(
                    "Клиент подъезжает — пока положите мешок клавишей G",
                    false);
                return;
            }

            if (customerVisit.isCustomerVisitDeparting ||
                customerVisit.isCustomerVisitCompleted)
            {
                player.SetInteractionPrompt(
                    "Клиент уезжает — положите мешок клавишей G",
                    false);
                return;
            }

            if (customerVisit.isCustomerVisitWaiting)
            {
                player.SetInteractionPrompt("Сначала примите заказ у стойки", false);
                return;
            }

            if (!customerVisit.isCustomerVisitLoading)
            {
                throw new InvalidOperationException(
                    $"Customer visit {customerVisit.EntityId} has no valid lifecycle state.");
            }

            player.SetInteractionPrompt(
                heldProduct.ProductType == customerVisit.RequiredProductType
                    ? "Отнесите мешок в машину клиента"
                    : "Для активного заказа нужен другой товар",
                false);
        }
    }
}
