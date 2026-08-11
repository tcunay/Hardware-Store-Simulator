using System;
using System.Linq;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Localization;

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
                        LocalizedTexts.Text(LocalizationKey.PromptInboundOnlyAtIntake),
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
                        LocalizedTexts.Text(LocalizationKey.PromptStorageFull),
                        false);
                    continue;
                }

                player.SetInteractionPrompt(
                    LocalizedTexts.Text(
                        LocalizationKey.PromptStoreInboundProduct,
                        LocalizedTexts.ProductName(heldProduct.ProductType)),
                    true);
            }
        }

        private void ResolveHeldStockPrompt(
            GameEntity player,
            GameEntity store,
            GameEntity heldProduct)
        {
            LocalizedText productName = LocalizedTexts.ProductName(heldProduct.ProductType);
            GameEntity customerVisit =
                _gameContext.GetEntityWithCustomerVisitStoreEntityId(store.EntityId);
            if (customerVisit == null)
            {
                player.SetInteractionPrompt(
                    LocalizedTexts.Text(
                        LocalizationKey.PromptNoActiveOrderCarrying,
                        productName),
                    false);
                return;
            }

            if (customerVisit.isCustomerVisitArriving)
            {
                player.SetInteractionPrompt(
                    LocalizedTexts.Text(
                        LocalizationKey.PromptArrivingCarrying,
                        productName),
                    false);
                return;
            }

            if (customerVisit.isCustomerVisitReturning ||
                customerVisit.isCustomerVisitDeparting ||
                customerVisit.isCustomerVisitCompleted)
            {
                player.SetInteractionPrompt(
                    LocalizedTexts.Text(
                        LocalizationKey.PromptCompletedCarrying,
                        productName),
                    false);
                return;
            }

            if (customerVisit.isCustomerVisitConsulting)
            {
                player.SetInteractionPrompt(
                    LocalizedTexts.Text(
                        LocalizationKey.PromptConsultFirstCarrying,
                        productName),
                    false);
                return;
            }

            if (customerVisit.isCustomerVisitWaiting)
            {
                player.SetInteractionPrompt(
                    LocalizedTexts.Text(LocalizationKey.PromptAcceptOrderFirst),
                    false);
                return;
            }

            if (!customerVisit.isCustomerVisitLoading)
            {
                throw new InvalidOperationException(
                    $"Customer visit {customerVisit.EntityId} has no valid lifecycle state.");
            }

            GameEntity[] lines = GetOrderLines(customerVisit);
            GameEntity matchingLine = lines.FirstOrDefault(line =>
                line.ProductType == heldProduct.ProductType);
            bool canLoad = matchingLine != null &&
                           matchingLine.LoadedProductCount <
                           matchingLine.RequiredProductCount;
            player.SetInteractionPrompt(
                canLoad
                    ? LocalizedTexts.Text(
                        LocalizationKey.PromptCarryToCustomerVehicle,
                        productName)
                    : matchingLine == null
                        ? LocalizedTexts.Text(
                            LocalizationKey.PromptProductNotInOrderCarrying,
                            productName)
                        : LocalizedTexts.Text(
                            LocalizationKey.PromptOrderLineAlreadyLoadedCarrying,
                            productName),
                false);
        }

        private GameEntity[] GetOrderLines(GameEntity order)
        {
            GameEntity[] lines = _gameContext
                .GetEntitiesWithOrderEntityId(order.EntityId)
                .Where(line => line.isOrderLine && !line.isDestructed)
                .OrderBy(line => line.LineIndex)
                .ToArray();
            if (lines.Length == 0)
                throw new InvalidOperationException(
                    $"Order {order.EntityId} has no active product lines.");

            return lines;
        }
    }
}
