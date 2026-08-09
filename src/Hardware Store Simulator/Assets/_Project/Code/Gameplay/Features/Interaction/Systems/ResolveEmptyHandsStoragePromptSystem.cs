using System;
using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveEmptyHandsStoragePromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;

        public ResolveEmptyHandsStoragePromptSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Player,
                    GameMatcher.StoreEntityId,
                    GameMatcher.FocusedEntityId,
                    GameMatcher.FocusedInteractionType)
                .NoneOf(GameMatcher.HeldProductId));
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
                if (!storageZone.isStorageZone || !storageZone.hasSlots)
                    throw new InvalidOperationException(
                        $"Entity {storageZone.EntityId} is not a configured storage zone.");

                GameEntity store = _gameContext.RequireStore(player);
                if (storageZone.EntityId != store.StorageZoneEntityId)
                    continue;

                GameEntity terminal = _gameContext.GetRequiredEntity(
                    store.ProcurementTerminalEntityId,
                    "store procurement terminal");
                if (!terminal.isProcurementTerminal ||
                    terminal.StoreEntityId != store.EntityId ||
                    terminal.StorageZoneEntityId != storageZone.EntityId)
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} has an invalid procurement terminal relation.");

                if (terminal.hasDeliveryEntityId)
                {
                    GameEntity delivery = _gameContext.GetRequiredEntity(
                        terminal.DeliveryEntityId,
                        "terminal active delivery");
                    if (!delivery.isDelivery || !delivery.isDeliveryActive)
                        throw new InvalidOperationException(
                            $"Procurement terminal {terminal.EntityId} has a stale delivery relation.");

                    player.SetInteractionPrompt(
                        "Принесите сюда мешок из машины поставщика",
                        false);
                    continue;
                }

                GameEntity order = _gameContext.GetRequiredEntity(
                    store.OrderEntityId,
                    "store order");
                if (!order.isOrder ||
                    order.StoreEntityId != store.EntityId ||
                    order.StorageZoneEntityId != storageZone.EntityId)
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} has an invalid order relation.");

                if (order.isOrderActive)
                {
                    player.SetInteractionPrompt(
                        order.AvailableProductCount > 0
                            ? "Наведите прицел на мешок на складе и нажмите E"
                            : "Склад пуст — закажите поставку в терминале закупок",
                        false);
                    continue;
                }

                if (order.isOrderWaiting)
                {
                    if (order.AvailableProductCount >= order.RequiredProductCount)
                    {
                        player.SetInteractionPrompt(
                            "Товар на складе — примите заказ у стойки клиента",
                            false);
                    }
                    else if (order.AvailableProductCount > 0)
                    {
                        player.SetInteractionPrompt(
                            $"Для заказа не хватает товара: на складе " +
                            $"{order.AvailableProductCount}/{order.RequiredProductCount}. " +
                            "Закажите поставку",
                            false);
                    }
                    else
                    {
                        player.SetInteractionPrompt(
                            "Склад пуст — закажите поставку в терминале закупок",
                            false);
                    }

                    continue;
                }

                if (!order.isOrderCompleted)
                    throw new InvalidOperationException(
                        $"Order {order.EntityId} has no valid lifecycle state.");

                player.SetInteractionPrompt(
                    order.AvailableProductCount > 0
                        ? $"Заказ выполнен — на складе осталось мешков: " +
                          $"{order.AvailableProductCount}"
                        : "Склад пуст — закажите поставку в терминале закупок",
                    false);
            }
        }
    }
}
