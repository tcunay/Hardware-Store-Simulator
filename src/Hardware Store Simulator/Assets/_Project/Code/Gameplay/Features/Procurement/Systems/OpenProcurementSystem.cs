using System;
using System.Linq;
using Entitas;
using HardwareStore.Gameplay.Common.Cursor;
using HardwareStore.Gameplay.Common.Economy;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Procurement.Systems
{
    public sealed class OpenProcurementSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly ICursorService _cursor;
        private readonly IStaticDataService _staticData;
        private readonly IProcurementSolvencyService _solvency;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public OpenProcurementSystem(GameContext gameContext, ICursorService cursor,
            IStaticDataService staticData, IGameEventFactory events,
            IProcurementSolvencyService solvency)
        {
            _gameContext = gameContext;
            _cursor = cursor;
            _staticData = staticData;
            _events = events;
            _solvency = solvency;
            _requests = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.InteractionRequest,
                GameMatcher.SourceEntityId,
                GameMatcher.TargetEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity request in _requests)
            {
                GameEntity terminal =
                    _gameContext.GetEntityWithEntityId(request.TargetEntityId);
                if (terminal == null)
                    throw new InvalidOperationException(
                        $"Interaction targets missing entity {request.TargetEntityId}.");
                if (!terminal.isProcurementTerminal)
                    continue;

                GameEntity player =
                    _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                if (player == null)
                    throw new InvalidOperationException(
                        $"Interaction source {request.SourceEntityId} does not exist.");
                Open(player, terminal);
            }
        }

        private void Open(GameEntity player, GameEntity terminal)
        {
            ValidateTerminal(terminal);
            if (!player.isPlayer || !player.hasEntityId ||
                !player.hasStoreEntityId || !player.hasMoveDirection)
            {
                throw new InvalidOperationException(
                    "Procurement interaction source is not a configured player.");
            }
            if (player.isModalOpen)
                return;
            if (player.isHandsOccupied)
            {
                _events.EmitNotification(LocalizedTexts.Text(
                    player.isPushingTrolley
                        ? LocalizationKey.NotificationReleaseTrolleyFirst
                        : LocalizationKey.NotificationFreeHandsForProcurement));
                return;
            }
            if (player.StoreEntityId != terminal.StoreEntityId)
                throw new InvalidOperationException(
                    $"Player {player.EntityId} cannot use procurement terminal " +
                    $"{terminal.EntityId} of another store.");
            if (player.hasConsultationVisitEntityId ||
                player.hasProcurementTerminalEntityId ||
                player.hasDayReportStoreEntityId)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} has a modal relation without ModalOpen.");
            }
            if (_gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                    terminal.EntityId) != null)
            {
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationAcceptCurrentDeliveryFirst));
                return;
            }
            SelectOpeningProduct(terminal);

            player.AddProcurementTerminalEntityId(terminal.EntityId);
            player.isModalOpen = true;
            player.ReplaceMoveDirection(Vector3.zero);
            if (player.hasInteractionPrompt)
                player.RemoveInteractionPrompt();
            player.isFocusInteractionAvailable = false;
            if (player.hasFocusedInteractionType)
                player.RemoveFocusedInteractionType();
            if (player.hasFocusedEntityId)
                player.RemoveFocusedEntityId();
            terminal.isHighlighted = false;
            player.isCursorLocked = true;
            _cursor.SetLocked(true);
        }

        private void ValidateTerminal(GameEntity terminal)
        {
            if (!terminal.hasEntityId || !terminal.hasStoreEntityId ||
                !terminal.hasStorageZoneEntityId || !terminal.hasSelectedProductType ||
                !terminal.hasDeliverySpawnPosition ||
                !terminal.hasDeliverySpawnRotation)
            {
                throw new InvalidOperationException(
                    "Procurement terminal has incomplete domain configuration.");
            }

            GameEntity store = _gameContext.GetEntityWithEntityId(terminal.StoreEntityId);
            GameEntity storageZone = _gameContext.GetEntityWithEntityId(
                terminal.StorageZoneEntityId);
            if (store == null || !store.isStore || !store.hasMoney ||
                !store.hasProcurementTerminalEntityId ||
                store.ProcurementTerminalEntityId != terminal.EntityId ||
                !store.hasStorageZoneEntityId ||
                store.StorageZoneEntityId != terminal.StorageZoneEntityId)
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminal.EntityId} has an invalid store relation.");
            }
            if (storageZone == null || !storageZone.isStorageZone ||
                !storageZone.hasSlots || !storageZone.hasOccupiedStorageSlotCount)
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminal.EntityId} has an invalid storage relation.");
            }
        }

        private void SelectOpeningProduct(GameEntity terminal)
        {
            ProcurementPurchaseEvaluation evaluation = _solvency.EvaluatePurchase(
                terminal.EntityId,
                terminal.SelectedProductType);
            if (evaluation.DemandKind == ProcurementDemandKind.ProjectForecast)
            {
                SelectForecastProduct(terminal, evaluation.ProjectType);
                return;
            }

            if (!evaluation.DemandVisitEntityId.HasValue)
                throw new InvalidOperationException(
                    $"Terminal {terminal.EntityId} resolved confirmed demand without a visit.");

            GameEntity visit = _gameContext.GetEntityWithEntityId(
                evaluation.DemandVisitEntityId.Value);
            ValidateConfirmedDemand(terminal, evaluation, visit);

            var indexedLines = _gameContext.GetEntitiesWithOrderEntityId(visit.EntityId);
            foreach (GameEntity line in indexedLines)
            {
                if (!line.hasLineIndex)
                {
                    throw new InvalidOperationException(
                        $"Order {visit.EntityId} contains a line without an index.");
                }
            }

            GameEntity[] lines = indexedLines
                .OrderBy(line => line.LineIndex)
                .ToArray();
            ValidateOrderLines(visit, lines);
            if (HasDeficit(lines, terminal.SelectedProductType))
                return;

            foreach (ProductTypeId productType in _staticData.ProductTypes)
            {
                if (!HasDeficit(lines, productType))
                    continue;

                terminal.ReplaceSelectedProductType(productType);
                return;
            }
        }

        private static void ValidateConfirmedDemand(
            GameEntity terminal,
            ProcurementPurchaseEvaluation evaluation,
            GameEntity visit)
        {
            if (visit == null || !visit.isCustomerVisit || visit.isDestructed ||
                !visit.isOrder || visit.isOrderRewarded || !visit.hasEntityId ||
                !visit.hasCustomerVisitStoreEntityId ||
                !visit.hasCustomerProjectType || !visit.hasStorageZoneEntityId ||
                !visit.hasCustomerArrivalSequence ||
                visit.CustomerVisitStoreEntityId != terminal.StoreEntityId ||
                visit.CustomerProjectType != evaluation.ProjectType ||
                visit.StorageZoneEntityId != terminal.StorageZoneEntityId)
            {
                throw new InvalidOperationException(
                    $"Terminal {terminal.EntityId} resolved an invalid confirmed demand visit.");
            }

            ValidateVisitLifecycle(visit);
        }

        private static void ValidateVisitLifecycle(GameEntity visit)
        {
            int lifecycleCount = 0;
            if (visit.isCustomerVisitArriving) lifecycleCount++;
            if (visit.isCustomerVisitQueued) lifecycleCount++;
            if (visit.isCustomerVisitConsulting) lifecycleCount++;
            if (visit.isCustomerVisitWaitingForLoadingBay) lifecycleCount++;
            if (visit.isCustomerVisitMovingToLoadingBay) lifecycleCount++;
            if (visit.isCustomerVisitLoading) lifecycleCount++;
            if (visit.isCustomerVisitCompleted) lifecycleCount++;
            if (visit.isCustomerVisitReturning) lifecycleCount++;
            if (visit.isCustomerVisitDeparting) lifecycleCount++;
            if (visit.isCustomerVisitAbandoning) lifecycleCount++;
            if (visit.isCustomerVisitWaitingForAbandonDeparture) lifecycleCount++;
            if (visit.isCustomerVisitAbandonDeparting) lifecycleCount++;
            if (lifecycleCount != 1)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} must have exactly one lifecycle marker.");
            }
        }

        private void SelectForecastProduct(
            GameEntity terminal,
            CustomerProjectTypeId projectType)
        {
            CustomerProjectConfig project = _staticData.GetProject(projectType);
            ProductTypeId firstProjectProduct = default;
            bool hasFirstProduct = false;
            bool selectedBelongsToProject = false;
            foreach (CustomerProjectOfferDefinition offer in project.Offers)
            foreach (CustomerProjectLineDefinition line in offer.Lines)
            {
                if (!hasFirstProduct)
                {
                    firstProjectProduct = line.ProductType;
                    hasFirstProduct = true;
                }
                if (line.ProductType == terminal.SelectedProductType)
                    selectedBelongsToProject = true;
            }

            if (!hasFirstProduct)
            {
                throw new InvalidOperationException(
                    $"Forecast project {projectType} has no configured product lines.");
            }
            if (!selectedBelongsToProject)
                terminal.ReplaceSelectedProductType(firstProjectProduct);
        }

        private static void ValidateOrderLines(GameEntity visit, GameEntity[] lines)
        {
            if (lines.Length == 0)
                throw new InvalidOperationException(
                    $"Order {visit.EntityId} has no active product lines.");

            for (int index = 0; index < lines.Length; index++)
            {
                GameEntity line = lines[index];
                if (!line.isOrderLine || line.isDestructed || !line.hasEntityId ||
                    !line.hasOrderEntityId ||
                    line.OrderEntityId != visit.EntityId || !line.hasProductType ||
                    !line.hasStorageZoneEntityId ||
                    line.StorageZoneEntityId != visit.StorageZoneEntityId ||
                    !line.hasLineIndex || line.LineIndex != index ||
                    !line.hasRequiredProductCount || !line.hasAvailableProductCount ||
                    !line.hasLoadedProductCount || line.RequiredProductCount <= 0 ||
                    line.AvailableProductCount < 0 || line.LoadedProductCount < 0 ||
                    line.LoadedProductCount > line.RequiredProductCount)
                {
                    throw new InvalidOperationException(
                        $"Order line {line.EntityId} has invalid procurement state.");
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (lines[previous].ProductType == line.ProductType)
                        throw new InvalidOperationException(
                            $"Order {visit.EntityId} contains duplicate product type " +
                            $"{line.ProductType}.");
                }
            }
        }

        private static bool HasDeficit(GameEntity[] lines, ProductTypeId productType)
        {
            foreach (GameEntity line in lines)
            {
                if (line.ProductType != productType)
                    continue;

                int remainingCount =
                    line.RequiredProductCount - line.LoadedProductCount;
                return remainingCount - line.AvailableProductCount > 0;
            }

            return false;
        }
    }
}
