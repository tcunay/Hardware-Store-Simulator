using Entitas;
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

                GameEntity terminal =
                    _gameContext.GetEntityWithEntityId(player.FocusedEntityId);
                if (terminal.StoreEntityId != player.StoreEntityId)
                    continue;

                GameEntity store =
                    _gameContext.GetEntityWithEntityId(terminal.StoreEntityId);
                GameEntity storageZone =
                    _gameContext.GetEntityWithEntityId(terminal.StorageZoneEntityId);

                GameEntity delivery =
                    _gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                        terminal.EntityId);
                if (delivery != null)
                {
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
