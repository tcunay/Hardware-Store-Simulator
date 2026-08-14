using System;
using System.Linq;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Presentation;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Presentation.Systems
{
    public sealed class PresentHudSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IStaticDataService _staticData;
        private readonly IHudService _hud;
        private readonly IGroup<GameEntity> _players;

        public PresentHudSystem(GameContext gameContext, IStaticDataService staticData,
            IHudService hud)
        {
            _gameContext = gameContext;
            _staticData = staticData;
            _hud = hud;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.EntityId,
                GameMatcher.StoreEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
                Present(player);
        }

        private void Present(GameEntity player)
        {
            GameEntity store = _gameContext.GetEntityWithEntityId(player.StoreEntityId);
            GameEntity procurementTerminal = _gameContext.GetEntityWithEntityId(
                store.ProcurementTerminalEntityId);
            GameEntity storageZone = _gameContext.GetEntityWithEntityId(
                store.StorageZoneEntityId);
            GameEntity delivery =
                _gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                    procurementTerminal.EntityId);

            bool hasActiveDelivery = delivery != null;
            ProductTypeId deliveryProductType = hasActiveDelivery
                ? delivery.ProductType
                : procurementTerminal.SelectedProductType;
            int deliveryStockedCount = hasActiveDelivery ? delivery.StockedProductCount : 0;
            int deliveryProductCount = hasActiveDelivery
                ? delivery.DeliveryProductCount
                : _staticData.GetDelivery(deliveryProductType).ProductCount;

            HudOrderState orderState = HudOrderState.NoCustomer;
            CustomerProjectTypeId? projectType = null;
            OrderLineSnapshot[] orderLines = Array.Empty<OrderLineSnapshot>();
            int totalAvailableProductCount = 0;
            int totalLoadedProductCount = 0;
            int totalRequiredProductCount = 0;
            GameEntity[] customerVisits = GetOrderedCustomerVisits(store);
            CustomerFlowSnapshot customerFlow = CreateCustomerFlowSnapshot(customerVisits);
            GameEntity customerVisit = ResolvePresentedCustomerVisit(customerVisits);
            if (customerVisit != null)
            {
                orderState = ResolveOrderState(customerVisit);
                projectType = customerVisit.CustomerProjectType;
                bool lifecycleRequiresOrder = LifecycleRequiresOrder(customerVisit);
                if (customerVisit.isOrder != lifecycleRequiresOrder)
                {
                    throw new InvalidOperationException(
                        $"Customer visit {customerVisit.EntityId} has an order that does " +
                        "not match its lifecycle state.");
                }

                GameEntity[] lineEntities = _gameContext
                    .GetEntitiesWithOrderEntityId(customerVisit.EntityId)
                    .Where(line => line.isOrderLine && !line.isDestructed)
                    .OrderBy(line => line.LineIndex)
                    .ToArray();
                if (lifecycleRequiresOrder)
                {
                    ValidateOrderLines(customerVisit, lineEntities);
                    orderLines = CreateLineSnapshots(lineEntities,
                        out totalAvailableProductCount,
                        out totalLoadedProductCount,
                        out totalRequiredProductCount);
                }
                else if (lineEntities.Length != 0)
                {
                    throw new InvalidOperationException(
                        $"Pre-order customer visit {customerVisit.EntityId} already contains " +
                        $"{lineEntities.Length} order lines.");
                }
            }

            ProductTypeId? carriedProductType = null;
            if (player.isCarryingProduct)
            {
                if (!player.isHandsOccupied || player.isPushingTrolley)
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} has invalid product handling state.");
                GameEntity carriedProduct =
                    _gameContext.GetEntityWithCarrierEntityId(player.EntityId);
                carriedProductType = carriedProduct.ProductType;
            }
            else if (player.isHandsOccupied != player.isPushingTrolley)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} has invalid trolley handling state.");
            }

            WarehouseWorkerStatusSnapshot? warehouseWorkerStatus =
                CreateWarehouseWorkerStatusSnapshot(store);

            _hud.Present(new HudSnapshot(
                CreateDayClockSnapshot(store),
                orderState,
                projectType,
                orderLines,
                totalAvailableProductCount,
                totalLoadedProductCount,
                totalRequiredProductCount,
                store.Money,
                storageZone.StorageProductCount,
                hasActiveDelivery,
                deliveryProductType,
                deliveryStockedCount,
                deliveryProductCount,
                carriedProductType,
                player.hasInteractionPrompt ? player.InteractionPrompt : null,
                player.hasFocusedEntityId,
                player.isFocusInteractionAvailable,
                player.isCarryingProduct,
                player.isPushingTrolley,
                player.isCursorLocked,
                customerFlow,
                warehouseWorkerStatus));
        }

        private GameEntity[] GetOrderedCustomerVisits(GameEntity store)
        {
            GameEntity[] visits = _gameContext
                .GetEntitiesWithCustomerVisitStoreEntityId(store.EntityId)
                .ToArray();
            foreach (GameEntity visit in visits)
                ValidateVisit(visit, store);

            visits = visits
                .OrderBy(visit => visit.CustomerArrivalSequence)
                .ThenBy(visit => visit.EntityId)
                .ToArray();
            int previousArrivalSequence = -1;
            foreach (GameEntity visit in visits)
            {
                if (visit.CustomerArrivalSequence == previousArrivalSequence)
                {
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} has duplicate customer arrival sequence " +
                        $"{visit.CustomerArrivalSequence}.");
                }

                previousArrivalSequence = visit.CustomerArrivalSequence;
            }

            return visits;
        }

        private static GameEntity ResolvePresentedCustomerVisit(GameEntity[] visits)
        {
            for (int index = 0; index < visits.Length; index++)
            {
                if (!IsAbandoning(visits[index]) &&
                    !visits[index].isOrderRewarded)
                    return visits[index];
            }

            for (int index = 0; index < visits.Length; index++)
            {
                if (!IsAbandoning(visits[index]))
                    return visits[index];
            }

            return null;
        }

        private static bool IsAbandoning(GameEntity visit) =>
            visit.isCustomerVisitAbandoning ||
            visit.isCustomerVisitWaitingForAbandonDeparture ||
            visit.isCustomerVisitAbandonDeparting;

        private static CustomerFlowSnapshot CreateCustomerFlowSnapshot(
            GameEntity[] visits)
        {
            int arrivingCount = 0;
            int queuedCount = 0;
            int consultingCount = 0;
            int loadingPipelineCount = 0;
            int leavingCount = 0;
            foreach (GameEntity visit in visits)
            {
                if (visit.isCustomerVisitArriving)
                    arrivingCount = checked(arrivingCount + 1);
                else if (visit.isCustomerVisitQueued)
                    queuedCount = checked(queuedCount + 1);
                else if (visit.isCustomerVisitConsulting)
                    consultingCount = checked(consultingCount + 1);
                else if (visit.isCustomerVisitReturning ||
                         visit.isCustomerVisitWaitingForLoadingBay ||
                         visit.isCustomerVisitMovingToLoadingBay ||
                         visit.isCustomerVisitLoading)
                {
                    loadingPipelineCount = checked(loadingPipelineCount + 1);
                }
                else if (visit.isCustomerVisitCompleted ||
                         visit.isCustomerVisitDeparting ||
                         visit.isCustomerVisitAbandoning ||
                         visit.isCustomerVisitWaitingForAbandonDeparture ||
                         visit.isCustomerVisitAbandonDeparting)
                {
                    leavingCount = checked(leavingCount + 1);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has no countable lifecycle state.");
                }
            }

            return new CustomerFlowSnapshot(
                visits.Length,
                arrivingCount,
                queuedCount,
                consultingCount,
                loadingPipelineCount,
                leavingCount);
        }

        private WarehouseWorkerStatusSnapshot? CreateWarehouseWorkerStatusSnapshot(
            GameEntity store)
        {
            GameEntity worker =
                _gameContext.GetEntityWithWarehouseWorkerStoreEntityId(store.EntityId);
            if (worker == null)
                return null;
            if (!worker.isWarehouseWorker || worker.isDestructed || !worker.hasEntityId ||
                !worker.hasWarehouseWorkerStoreEntityId ||
                worker.WarehouseWorkerStoreEntityId != store.EntityId ||
                !worker.hasWarehouseWorkerStatus)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid warehouse worker.");
            }

            WarehouseWorkerStatusId status = worker.WarehouseWorkerStatus;
            GameEntity task =
                _gameContext.GetEntityWithAssignedWorkerEntityId(worker.EntityId);
            bool requiresTaskProduct = status is WarehouseWorkerStatusId.MovingToPickup or
                WarehouseWorkerStatusId.MovingToStorage;
            if (!requiresTaskProduct)
            {
                if (task != null)
                {
                    throw new InvalidOperationException(
                        $"Warehouse worker {worker.EntityId} has an unexpected task while " +
                        $"presenting status {status}.");
                }

                return new WarehouseWorkerStatusSnapshot(status, null);
            }

            if (task == null || !task.isWarehouseTask || task.isDestructed ||
                !task.hasWarehouseTaskProductEntityId ||
                !task.hasAssignedWorkerEntityId ||
                task.AssignedWorkerEntityId != worker.EntityId)
            {
                throw new InvalidOperationException(
                    $"Moving warehouse worker {worker.EntityId} has no valid assigned task.");
            }

            GameEntity product = _gameContext.GetEntityWithEntityId(
                task.WarehouseTaskProductEntityId);
            if (product == null || product.isDestructed || !product.isProduct ||
                !product.hasProductType)
            {
                throw new InvalidOperationException(
                    $"Warehouse task {task.EntityId} references an invalid product.");
            }

            return new WarehouseWorkerStatusSnapshot(status, product.ProductType);
        }

        private static DayClockSnapshot CreateDayClockSnapshot(GameEntity store)
        {
            if (!store.hasDayNumber || !store.hasCurrentDayMinute)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has no configured day clock.");
            }

            int phaseCount =
                (store.isStorePreparing ? 1 : 0) +
                (store.isStoreOpen ? 1 : 0) +
                (store.isStoreClosing ? 1 : 0) +
                (store.isDayReportOpen ? 1 : 0);
            if (phaseCount != 1)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} must have exactly one day phase.");
            }

            float currentMinute = store.CurrentDayMinute;
            if (float.IsNaN(currentMinute) || float.IsInfinity(currentMinute) ||
                currentMinute < 0f || currentMinute >= 24f * 60f)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has invalid day minute {currentMinute}.");
            }

            StoreDayPhase phase = store.isStorePreparing
                ? StoreDayPhase.Preparing
                : store.isStoreOpen
                    ? StoreDayPhase.Open
                    : store.isStoreClosing
                        ? StoreDayPhase.Closing
                        : StoreDayPhase.Report;
            return new DayClockSnapshot(
                store.DayNumber,
                (int)Math.Floor(currentMinute),
                phase);
        }

        private OrderLineSnapshot[] CreateLineSnapshots(
            GameEntity[] lines,
            out int totalAvailableProductCount,
            out int totalLoadedProductCount,
            out int totalRequiredProductCount)
        {
            var snapshots = new OrderLineSnapshot[lines.Length];
            totalAvailableProductCount = 0;
            totalLoadedProductCount = 0;
            totalRequiredProductCount = 0;
            for (int index = 0; index < lines.Length; index++)
            {
                GameEntity line = lines[index];
                totalAvailableProductCount = checked(
                    totalAvailableProductCount +
                    Math.Min(line.AvailableProductCount, line.RequiredProductCount));
                totalLoadedProductCount = checked(
                    totalLoadedProductCount + line.LoadedProductCount);
                totalRequiredProductCount = checked(
                    totalRequiredProductCount + line.RequiredProductCount);
                snapshots[index] = new OrderLineSnapshot(
                    line.LineIndex,
                    line.ProductType,
                    line.AvailableProductCount,
                    line.LoadedProductCount,
                    line.RequiredProductCount);
            }

            return snapshots;
        }

        private static void ValidateVisit(GameEntity visit, GameEntity store)
        {
            if (!visit.isCustomerVisit || visit.isDestructed ||
                !visit.hasEntityId || !visit.hasCustomerVisitStoreEntityId ||
                !visit.hasCustomerProjectType || !visit.hasCustomerArrivalSequence ||
                visit.CustomerVisitStoreEntityId != store.EntityId ||
                visit.CustomerArrivalSequence < 0 ||
                !Enum.IsDefined(typeof(CustomerProjectTypeId),
                    visit.CustomerProjectType))
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid customer visit.");
            }

            ValidateSingleLifecycleState(visit);
            bool lifecycleRequiresOrder = LifecycleRequiresOrder(visit);
            if (visit.isOrder != lifecycleRequiresOrder)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an order that does not match " +
                    "its lifecycle state.");
            }
            bool lifecycleRequiresPatience =
                visit.isCustomerVisitArriving ||
                visit.isCustomerVisitQueued ||
                visit.isCustomerVisitConsulting;
            if (visit.hasCustomerPatienceRemaining != lifecycleRequiresPatience)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has patience that does not match " +
                    "its lifecycle state.");
            }
            if (lifecycleRequiresPatience)
            {
                float remaining = visit.CustomerPatienceRemaining;
                if (float.IsNaN(remaining) || float.IsInfinity(remaining) ||
                    remaining <= 0f)
                {
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has invalid patience " +
                        $"remaining {remaining}.");
                }
            }
            if (visit.isCustomerPatienceWarningIssued &&
                (!visit.hasCustomerPatienceRemaining ||
                 (!visit.isCustomerVisitQueued &&
                  !visit.isCustomerVisitConsulting)))
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has a patience warning marker " +
                    "outside its waiting lifecycle.");
            }
            bool lifecycleRequiresReward =
                visit.isCustomerVisitCompleted || visit.isCustomerVisitDeparting;
            if (visit.isOrderRewarded != lifecycleRequiresReward)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has a reward marker that does not " +
                    "match its lifecycle state.");
            }
        }

        private static void ValidateOrderLines(GameEntity visit, GameEntity[] lines)
        {
            if (lines.Length == 0 ||
                lines.Length > CustomerProjectConfig.MaxLinesPerOffer)
                throw new InvalidOperationException(
                    $"Order {visit.EntityId} must expose between one and " +
                    $"{CustomerProjectConfig.MaxLinesPerOffer} product lines, found " +
                    $"{lines.Length}.");

            for (int index = 0; index < lines.Length; index++)
            {
                GameEntity line = lines[index];
                if (!line.hasOrderEntityId || !line.hasLineIndex ||
                    !line.hasProductType || !line.hasRequiredProductCount ||
                    !line.hasAvailableProductCount || !line.hasLoadedProductCount)
                {
                    throw new InvalidOperationException(
                        $"Order line at position {index} for order {visit.EntityId} is not " +
                        "fully configured.");
                }
                if (line.OrderEntityId != visit.EntityId || line.LineIndex != index ||
                    line.RequiredProductCount <= 0 || line.AvailableProductCount < 0 ||
                    line.LoadedProductCount < 0 ||
                    line.LoadedProductCount > line.RequiredProductCount)
                {
                    throw new InvalidOperationException(
                        $"Order {visit.EntityId} has an invalid line at position {index}.");
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

        private static HudOrderState ResolveOrderState(GameEntity customerVisit)
        {
            if (customerVisit.isCustomerVisitArriving ||
                customerVisit.isCustomerVisitQueued)
                return HudOrderState.Arriving;
            if (customerVisit.isCustomerVisitConsulting)
                return HudOrderState.Consulting;
            if (customerVisit.isCustomerVisitReturning)
                return HudOrderState.Returning;
            if (customerVisit.isCustomerVisitDeparting)
                return HudOrderState.Departing;
            if (customerVisit.isCustomerVisitWaitingForLoadingBay ||
                customerVisit.isCustomerVisitMovingToLoadingBay ||
                customerVisit.isCustomerVisitLoading)
                return HudOrderState.Active;
            if (customerVisit.isCustomerVisitCompleted)
                return HudOrderState.Completed;

            throw new InvalidOperationException(
                $"Customer visit {customerVisit.EntityId} has no lifecycle state.");
        }

        private static void ValidateSingleLifecycleState(GameEntity customerVisit)
        {
            int lifecycleStateCount =
                (customerVisit.isCustomerVisitArriving ? 1 : 0) +
                (customerVisit.isCustomerVisitQueued ? 1 : 0) +
                (customerVisit.isCustomerVisitConsulting ? 1 : 0) +
                (customerVisit.isCustomerVisitWaitingForLoadingBay ? 1 : 0) +
                (customerVisit.isCustomerVisitMovingToLoadingBay ? 1 : 0) +
                (customerVisit.isCustomerVisitLoading ? 1 : 0) +
                (customerVisit.isCustomerVisitCompleted ? 1 : 0) +
                (customerVisit.isCustomerVisitReturning ? 1 : 0) +
                (customerVisit.isCustomerVisitDeparting ? 1 : 0) +
                (customerVisit.isCustomerVisitAbandoning ? 1 : 0) +
                (customerVisit.isCustomerVisitWaitingForAbandonDeparture ? 1 : 0) +
                (customerVisit.isCustomerVisitAbandonDeparting ? 1 : 0);
            if (lifecycleStateCount != 1)
                throw new InvalidOperationException(
                    $"Customer visit {customerVisit.EntityId} must have exactly one " +
                    "lifecycle state.");
        }

        private static bool LifecycleRequiresOrder(GameEntity customerVisit) =>
            customerVisit.isCustomerVisitWaitingForLoadingBay ||
            customerVisit.isCustomerVisitMovingToLoadingBay ||
            customerVisit.isCustomerVisitLoading ||
            customerVisit.isCustomerVisitCompleted ||
            customerVisit.isCustomerVisitReturning ||
            customerVisit.isCustomerVisitDeparting;
    }
}
