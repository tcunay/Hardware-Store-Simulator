using System;
using System.Linq;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Localization;

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
                GameEntity terminal = _gameContext.GetEntityWithEntityId(
                    store.ProcurementTerminalEntityId);
                GameEntity delivery =
                    _gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                        terminal.EntityId);
                if (delivery != null)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            LocalizationKey.PromptBringDeliveryToIntake,
                            LocalizedTexts.ProductName(delivery.ProductType)),
                        false);
                    continue;
                }

                GameEntity customerVisit =
                    _gameContext.GetCurrentLoadingVisit(store.EntityId);
                if (customerVisit == null)
                {
                    player.SetInteractionPrompt(
                        storageZone.StorageProductCount > 0
                            ? LocalizedTexts.Text(
                                LocalizationKey.PromptStorageCountWaitCustomer,
                                storageZone.StorageProductCount)
                            : LocalizedTexts.Text(
                                LocalizationKey.PromptStorageEmptyOrderDelivery),
                        false);
                    continue;
                }
                GameEntity[] lines = GetOrderLines(customerVisit);
                GameEntity incompleteLine = lines.FirstOrDefault(line =>
                    line.LoadedProductCount < line.RequiredProductCount);
                if (incompleteLine == null)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(LocalizationKey.PromptAllLinesLoaded),
                        false);
                    continue;
                }

                GameEntity availableLine = lines.FirstOrDefault(line =>
                    line.LoadedProductCount < line.RequiredProductCount &&
                    line.AvailableProductCount > 0);
                GameEntity promptedLine = availableLine ?? incompleteLine;
                LocalizedText productName =
                    LocalizedTexts.ProductName(promptedLine.ProductType);
                player.SetInteractionPrompt(
                    availableLine != null
                        ? LocalizedTexts.Text(
                            LocalizationKey.PromptFocusStockProduct,
                            productName)
                        : LocalizedTexts.Text(
                            LocalizationKey.PromptNoStockForOrder,
                            productName),
                    false);
            }
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
