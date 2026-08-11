using System;
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
                GameMatcher.CarryingProduct,
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
                    if (heldProduct.hasStorageSlotIndex ||
                        !heldProduct.hasReservedStorageSlotIndex ||
                        !heldProduct.hasReservedOrderLineEntityId)
                    {
                        throw new InvalidOperationException(
                            $"Held stock product {heldProduct.EntityId} has invalid storage " +
                            "reservation state.");
                    }

                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            LocalizationKey.PromptReturnStockProduct,
                            LocalizedTexts.ProductName(heldProduct.ProductType)),
                        true);
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

    }
}
