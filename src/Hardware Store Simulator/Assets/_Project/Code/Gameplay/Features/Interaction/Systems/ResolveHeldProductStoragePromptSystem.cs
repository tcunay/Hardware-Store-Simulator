using System;
using Entitas;
using HardwareStore.Common.Entity;
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
                GameMatcher.StoreEntityId,
                GameMatcher.HeldProductId,
                GameMatcher.FocusedEntityId,
                GameMatcher.FocusedInteractionType));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                if (player.FocusedInteractionType != InteractionTypeId.StorageZone)
                    continue;

                GameEntity storageZone = _gameContext.GetRequiredEntity(
                    player.FocusedEntityId,
                    "focused storage zone");
                if (!storageZone.isStorageZone || !storageZone.hasSlots ||
                    !storageZone.hasOccupiedStorageSlotCount)
                    throw new InvalidOperationException(
                        $"Entity {storageZone.EntityId} is not a configured storage zone.");

                GameEntity store = _gameContext.RequireStore(player);
                if (storageZone.EntityId != store.StorageZoneEntityId)
                    continue;

                GameEntity heldProduct = _gameContext.GetRequiredEntity(
                    player.HeldProductId,
                    "player held product");
                if (!heldProduct.isProduct || !heldProduct.isCarried)
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} holds invalid product {heldProduct.EntityId}.");

                if (heldProduct.isInStock)
                {
                    if (!heldProduct.hasStorageZoneEntityId ||
                        heldProduct.StorageZoneEntityId != storageZone.EntityId)
                        throw new InvalidOperationException(
                            $"Held stock product {heldProduct.EntityId} does not belong to " +
                            $"storage zone {storageZone.EntityId}.");

                    player.SetInteractionPrompt(
                        "Отнесите мешок в машину клиента",
                        false);
                    continue;
                }

                if (!heldProduct.isInboundProduct)
                {
                    player.SetInteractionPrompt(
                        "На приёмку можно положить только товар из поставки",
                        false);
                    continue;
                }

                if (!heldProduct.hasDeliveryEntityId)
                    throw new InvalidOperationException(
                        $"Inbound product {heldProduct.EntityId} has no delivery relation.");

                GameEntity terminal = _gameContext.GetRequiredEntity(
                    store.ProcurementTerminalEntityId,
                    "store procurement terminal");
                if (!terminal.isProcurementTerminal ||
                    terminal.StoreEntityId != store.EntityId ||
                    terminal.StorageZoneEntityId != storageZone.EntityId)
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} has an invalid procurement terminal relation.");
                if (!terminal.hasDeliveryEntityId ||
                    terminal.DeliveryEntityId != heldProduct.DeliveryEntityId)
                    throw new InvalidOperationException(
                        $"Inbound product {heldProduct.EntityId} does not belong to the active " +
                        $"delivery of procurement terminal {terminal.EntityId}.");

                GameEntity delivery = _gameContext.GetRequiredEntity(
                    terminal.DeliveryEntityId,
                    "terminal active delivery");
                if (!delivery.isDelivery || !delivery.isDeliveryActive)
                    throw new InvalidOperationException(
                        $"Procurement terminal {terminal.EntityId} has a stale delivery relation.");

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
    }
}
