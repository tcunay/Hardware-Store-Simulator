using System;
using System.Linq;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveLoadingZonePromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;

        public ResolveLoadingZonePromptSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.EntityId,
                GameMatcher.StoreEntityId,
                GameMatcher.FocusedEntityId,
                GameMatcher.FocusedInteractionType));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                if (player.FocusedInteractionType != InteractionTypeId.LoadingZone)
                    continue;

                GameEntity loadingZone =
                    _gameContext.GetEntityWithEntityId(player.FocusedEntityId);
                ValidateVisit(loadingZone, player.StoreEntityId);
                if (loadingZone.CustomerVisitStoreEntityId != player.StoreEntityId)
                    continue;
                if (loadingZone.isCustomerVisitArriving ||
                    loadingZone.isCustomerVisitQueued ||
                    loadingZone.isCustomerVisitWaitingForLoadingBay ||
                    loadingZone.isCustomerVisitMovingToLoadingBay)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(LocalizationKey.PromptLoadingWaitConsultation),
                        false);
                    continue;
                }

                if (loadingZone.isCustomerVisitDeparting)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            LocalizationKey.PromptLoadingCompletedVehicleDeparting),
                        false);
                    continue;
                }

                if (loadingZone.isCustomerVisitAbandoning ||
                    loadingZone.isCustomerVisitWaitingForAbandonDeparture ||
                    loadingZone.isCustomerVisitAbandonDeparting)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            LocalizationKey.PromptCustomerLeftImpatient),
                        false);
                    continue;
                }

                if (loadingZone.isCustomerVisitReturning)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            LocalizationKey.PromptLoadingCompletedCustomerReturning),
                        false);
                    continue;
                }

                if (loadingZone.isCustomerVisitConsulting)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(LocalizationKey.PromptLoadingConsultFirst),
                        false);
                    continue;
                }

                if (loadingZone.isCustomerVisitCompleted)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(LocalizationKey.PromptLoadingVehicleFull),
                        false);
                    continue;
                }

                if (!loadingZone.isCustomerVisitLoading)
                    throw new InvalidOperationException(
                        $"Customer visit {loadingZone.EntityId} has no valid lifecycle state.");

                GameEntity currentLoadingVisit =
                    _gameContext.GetCurrentLoadingVisit(player.StoreEntityId);
                if (currentLoadingVisit == null ||
                    currentLoadingVisit.EntityId != loadingZone.EntityId)
                {
                    throw new InvalidOperationException(
                        $"Loading-zone visit {loadingZone.EntityId} does not reserve store " +
                        $"{player.StoreEntityId} customer loading bay.");
                }

                GameEntity[] lines = GetOrderLines(loadingZone);
                if (player.isPushingTrolley)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(LocalizationKey.PromptReleaseTrolleyFirst),
                        false);
                    continue;
                }
                if (!player.isHandsOccupied)
                {
                    GameEntity incompleteLine = lines.FirstOrDefault(line =>
                        line.LoadedProductCount < line.RequiredProductCount);
                    if (incompleteLine == null)
                    {
                        player.SetInteractionPrompt(
                            LocalizedTexts.Text(
                                LocalizationKey.PromptLoadingAllLinesLoaded),
                            false);
                        continue;
                    }

                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            LocalizationKey.PromptBringProductFromStorage,
                            LocalizedTexts.ProductName(incompleteLine.ProductType),
                            incompleteLine.LoadedProductCount,
                            incompleteLine.RequiredProductCount),
                        false);
                    continue;
                }

                GameEntity heldProduct =
                    _gameContext.GetEntityWithCarrierEntityId(player.EntityId);
                GameEntity matchingLine = lines.FirstOrDefault(line =>
                    line.ProductType == heldProduct.ProductType);
                if (heldProduct.isInStock &&
                    (!heldProduct.hasReservedStorageSlotIndex ||
                     !heldProduct.hasReservedOrderLineEntityId ||
                     matchingLine == null ||
                     heldProduct.ReservedOrderLineEntityId != matchingLine.EntityId))
                {
                    throw new InvalidOperationException(
                        $"Held stock product {heldProduct.EntityId} has no matching active " +
                        "order-line reservation.");
                }

                bool available = heldProduct.isInStock &&
                                 heldProduct.StorageZoneEntityId ==
                                 loadingZone.StorageZoneEntityId &&
                                 matchingLine != null &&
                                 heldProduct.hasReservedStorageSlotIndex &&
                                 heldProduct.hasReservedOrderLineEntityId &&
                                 heldProduct.ReservedOrderLineEntityId ==
                                 matchingLine.EntityId &&
                                 matchingLine.LoadedProductCount <
                                 matchingLine.RequiredProductCount;
                if (!available)
                {
                    LocalizedText productName =
                        LocalizedTexts.ProductName(heldProduct.ProductType);
                    player.SetInteractionPrompt(
                        matchingLine == null
                            ? LocalizedTexts.Text(
                                LocalizationKey.PromptLoadingProductNotInOrder,
                                productName)
                            : matchingLine.LoadedProductCount >=
                              matchingLine.RequiredProductCount
                                ? LocalizedTexts.Text(
                                    LocalizationKey.PromptLoadingLineAlreadyLoaded,
                                    productName)
                                : LocalizedTexts.Text(
                                    LocalizationKey.PromptProductMustComeFromStorage,
                                    productName),
                        false);
                    continue;
                }

                int loadedUnitCount = lines.Sum(line => line.LoadedProductCount);
                if (loadingZone.Slots.Length <= loadedUnitCount)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            LocalizationKey.PromptCustomerVehicleNoSpace),
                        false);
                    continue;
                }

                player.SetInteractionPrompt(
                    LocalizedTexts.Text(
                        LocalizationKey.PromptLoadProduct,
                        LocalizedTexts.ProductName(heldProduct.ProductType),
                        matchingLine.LoadedProductCount,
                        matchingLine.RequiredProductCount),
                    true);
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

        private static void ValidateVisit(GameEntity visit, int storeEntityId)
        {
            if (visit == null || visit.isDestructed || !visit.isCustomerVisit ||
                !visit.isCustomerVehicle || !visit.isLoadingZone || !visit.hasEntityId ||
                !visit.hasCustomerVisitStoreEntityId ||
                visit.CustomerVisitStoreEntityId != storeEntityId)
            {
                throw new InvalidOperationException(
                    $"Player store {storeEntityId} focuses an invalid customer loading zone.");
            }

            int lifecycleCount =
                (visit.isCustomerVisitArriving ? 1 : 0) +
                (visit.isCustomerVisitQueued ? 1 : 0) +
                (visit.isCustomerVisitConsulting ? 1 : 0) +
                (visit.isCustomerVisitWaitingForLoadingBay ? 1 : 0) +
                (visit.isCustomerVisitMovingToLoadingBay ? 1 : 0) +
                (visit.isCustomerVisitLoading ? 1 : 0) +
                (visit.isCustomerVisitCompleted ? 1 : 0) +
                (visit.isCustomerVisitReturning ? 1 : 0) +
                (visit.isCustomerVisitDeparting ? 1 : 0) +
                (visit.isCustomerVisitAbandoning ? 1 : 0) +
                (visit.isCustomerVisitWaitingForAbandonDeparture ? 1 : 0) +
                (visit.isCustomerVisitAbandonDeparting ? 1 : 0);
            if (lifecycleCount != 1)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} must have exactly one lifecycle marker.");
            }
        }
    }
}
