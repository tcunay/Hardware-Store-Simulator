using System;
using Entitas;
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
                .NoneOf(GameMatcher.HandsOccupied));
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
                GameEntity customerVisit =
                    _gameContext.GetEntityWithCustomerVisitStoreEntityId(store.EntityId);

                GameEntity terminal = _gameContext.GetEntityWithEntityId(
                    store.ProcurementTerminalEntityId);
                if (_gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                        terminal.EntityId) != null)
                {
                    player.SetInteractionPrompt(
                        "Принесите сюда мешок из машины поставщика",
                        false);
                    continue;
                }

                if (customerVisit == null)
                {
                    player.SetInteractionPrompt(
                        storageZone.StorageProductCount > 0
                            ? $"На складе мешков: {storageZone.StorageProductCount}. " +
                              "Ожидайте следующего клиента"
                            : "Склад пуст — можно заказать поставку",
                        false);
                    continue;
                }
                if (customerVisit.isCustomerVisitArriving)
                {
                    player.SetInteractionPrompt(
                        "Клиент подъезжает — можно подготовить товар",
                        false);
                    continue;
                }

                if (customerVisit.isCustomerVisitDeparting)
                {
                    player.SetInteractionPrompt(
                        "Клиент уезжает — ожидайте следующего",
                        false);
                    continue;
                }

                if (customerVisit.isCustomerVisitLoading)
                {
                    player.SetInteractionPrompt(
                        customerVisit.AvailableProductCount > 0
                            ? "Наведите прицел на мешок на складе и нажмите E"
                            : "Склад пуст — закажите поставку в терминале закупок",
                        false);
                    continue;
                }

                if (customerVisit.isCustomerVisitWaiting)
                {
                    if (customerVisit.AvailableProductCount >=
                        customerVisit.RequiredProductCount)
                    {
                        player.SetInteractionPrompt(
                            "Товар на складе — примите заказ у стойки клиента",
                            false);
                    }
                    else if (customerVisit.AvailableProductCount > 0)
                    {
                        player.SetInteractionPrompt(
                            $"Для заказа не хватает товара: на складе " +
                            $"{customerVisit.AvailableProductCount}/" +
                            $"{customerVisit.RequiredProductCount}. " +
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

                if (!customerVisit.isCustomerVisitCompleted)
                    throw new InvalidOperationException(
                        $"Customer visit {customerVisit.EntityId} has no valid lifecycle state.");

                player.SetInteractionPrompt(
                    customerVisit.AvailableProductCount > 0
                        ? $"Заказ выполнен — на складе осталось мешков: " +
                          $"{customerVisit.AvailableProductCount}"
                        : "Склад пуст — закажите поставку в терминале закупок",
                    false);
            }
        }
    }
}
