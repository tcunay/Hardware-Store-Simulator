using System;
using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveProcurementTerminalPromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;

        public ResolveProcurementTerminalPromptSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.StoreEntityId,
                GameMatcher.FocusedEntityId,
                GameMatcher.FocusedInteractionType));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                if (player.FocusedInteractionType != InteractionTypeId.ProcurementTerminal)
                    continue;

                GameEntity terminal = _gameContext.GetRequiredEntity(
                    player.FocusedEntityId,
                    "focused procurement terminal");
                if (!terminal.isProcurementTerminal)
                    throw new InvalidOperationException(
                        $"Entity {terminal.EntityId} is not a procurement terminal.");

                GameEntity store = _gameContext.RequireStore(player);
                if (terminal.EntityId != store.ProcurementTerminalEntityId ||
                    terminal.StoreEntityId != store.EntityId ||
                    terminal.StorageZoneEntityId != store.StorageZoneEntityId)
                    continue;

                GameEntity storageZone = _gameContext.GetRequiredEntity(
                    terminal.StorageZoneEntityId,
                    "terminal storage zone");
                if (!storageZone.isStorageZone || !storageZone.hasSlots ||
                    !storageZone.hasOccupiedStorageSlotCount)
                    throw new InvalidOperationException(
                        $"Entity {terminal.StorageZoneEntityId} is not a configured storage zone.");
                if (terminal.DeliveryProductCount <= 0 ||
                    terminal.DeliveryProductCount > storageZone.Slots.Length)
                    throw new InvalidOperationException(
                        $"Delivery size {terminal.DeliveryProductCount} does not fit storage " +
                        $"capacity {storageZone.Slots.Length}.");

                if (terminal.hasDeliveryEntityId)
                {
                    GameEntity delivery = _gameContext.GetRequiredEntity(
                        terminal.DeliveryEntityId,
                        "terminal active delivery");
                    if (!delivery.isDelivery || !delivery.isDeliveryActive)
                        throw new InvalidOperationException(
                            $"Procurement terminal {terminal.EntityId} has a stale delivery relation.");

                    player.SetInteractionPrompt(
                        $"Поставка разгружается: {delivery.StockedProductCount}/" +
                        $"{delivery.DeliveryProductCount} принято",
                        false);
                    continue;
                }

                int freeSlotCount =
                    storageZone.Slots.Length - storageZone.OccupiedStorageSlotCount;
                if (freeSlotCount < terminal.DeliveryProductCount)
                {
                    player.SetInteractionPrompt(
                        $"Недостаточно места на складе: свободно {freeSlotCount}/" +
                        $"{terminal.DeliveryProductCount}",
                        false);
                    continue;
                }

                if (store.Money < terminal.DeliveryCost)
                {
                    player.SetInteractionPrompt(
                        $"Недостаточно денег — нужно {terminal.DeliveryCost:N0} ₽",
                        false);
                    continue;
                }

                player.SetInteractionPrompt(
                    $"E — заказать {terminal.DeliveryProductCount} мешка цемента " +
                    $"за {terminal.DeliveryCost:N0} ₽",
                    true);
            }
        }
    }
}
