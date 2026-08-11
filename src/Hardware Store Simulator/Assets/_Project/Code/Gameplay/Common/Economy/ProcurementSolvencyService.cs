using System;
using System.Collections.Generic;
using System.Linq;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Common.Economy
{
    public sealed class ProcurementSolvencyService :
        IProcurementSolvencyService,
        IEconomySolvencyService
    {
        private const int MaximumProjectionLeafCount = 4096;

        private readonly GameContext _gameContext;
        private readonly IStaticDataService _staticData;
        private readonly IGroup<GameEntity> _stockedProducts;

        public ProcurementSolvencyService(
            GameContext gameContext,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _staticData = staticData;
            _stockedProducts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.EntityId,
                    GameMatcher.InStock,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.ProductType)
                .NoneOf(GameMatcher.Destructed));
        }

        public ProcurementPurchaseEvaluation EvaluatePurchase(
            int procurementTerminalEntityId,
            ProductTypeId productType)
        {
            GameEntity terminal =
                _gameContext.GetEntityWithEntityId(procurementTerminalEntityId);
            TerminalState terminalState = ValidateAndResolveTerminal(terminal);
            DemandTarget demand = ResolveDemand(terminalState.Store);
            DeliveryConfig candidateDelivery = _staticData.GetDelivery(productType);
            if (candidateDelivery.ProductType != productType)
            {
                throw new InvalidOperationException(
                    $"Delivery config for {productType} exposes " +
                    $"{candidateDelivery.ProductType}.");
            }

            ProjectionState state = CreateCurrentState(
                terminalState.Store,
                terminalState.StorageZone);
            Dictionary<ProductTypeId, int> exactRequirements =
                demand.Kind == ProcurementDemandKind.ConfirmedOrder
                    ? CollectRemainingOrderRequirements(demand.Order, state.Stock)
                    : null;
            int moneyAfterPurchase;
            try
            {
                moneyAfterPurchase = checked(state.Money - candidateDelivery.TotalCost);
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    "Procurement purchase totals must fit a 32-bit signed integer.",
                    exception);
            }

            ProcurementPurchaseAvailability availability;
            if (state.Capacity - state.OccupiedSlotCount <
                candidateDelivery.ProductCount)
            {
                availability = ProcurementPurchaseAvailability.InsufficientStorage;
            }
            else if (moneyAfterPurchase < 0)
            {
                availability = ProcurementPurchaseAvailability.InsufficientMoney;
            }
            else
            {
                ProjectionState afterCandidate = state.Clone();
                afterCandidate.Money = moneyAfterPurchase;
                afterCandidate.OccupiedSlotCount = checked(
                    afterCandidate.OccupiedSlotCount +
                    candidateDelivery.ProductCount);
                afterCandidate.Stock[productType] = checked(
                    afterCandidate.Stock[productType] +
                    candidateDelivery.ProductCount);

                availability = IsProtectedDemandSolvent(
                        afterCandidate,
                        demand,
                        exactRequirements)
                    ? ProcurementPurchaseAvailability.Available
                    : ProcurementPurchaseAvailability.DemandWouldBecomeInsolvent;
            }

            return new ProcurementPurchaseEvaluation(
                availability,
                demand.Kind,
                demand.ProjectType,
                candidateDelivery.ProductCount,
                candidateDelivery.TotalCost,
                moneyAfterPurchase);
        }

        public EconomyDebitEvaluation EvaluateDebit(int storeEntityId, int debitAmount)
        {
            if (debitAmount <= 0)
                throw new ArgumentOutOfRangeException(nameof(debitAmount));

            GameEntity store = _gameContext.GetEntityWithEntityId(storeEntityId);
            TerminalState terminalState = ValidateAndResolveStore(store);
            DemandTarget demand = ResolveDemand(store);
            ProjectionState state = CreateCurrentState(
                store,
                terminalState.StorageZone);
            Dictionary<ProductTypeId, int> exactRequirements =
                demand.Kind == ProcurementDemandKind.ConfirmedOrder
                    ? CollectRemainingOrderRequirements(demand.Order, state.Stock)
                    : null;
            IncludeCommittedDelivery(state, terminalState.Terminal);

            int moneyAfterDebit;
            try
            {
                moneyAfterDebit = checked(state.Money - debitAmount);
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    "Economy debit totals must fit a 32-bit signed integer.",
                    exception);
            }

            EconomyDebitAvailability availability;
            if (moneyAfterDebit < 0)
            {
                availability = EconomyDebitAvailability.InsufficientMoney;
            }
            else
            {
                state.Money = moneyAfterDebit;
                availability = IsProtectedDemandSolvent(
                        state,
                        demand,
                        exactRequirements)
                    ? EconomyDebitAvailability.Available
                    : EconomyDebitAvailability.DemandWouldBecomeInsolvent;
            }

            return new EconomyDebitEvaluation(availability, moneyAfterDebit);
        }

        private TerminalState ValidateAndResolveTerminal(GameEntity terminal)
        {
            if (terminal == null || !terminal.isProcurementTerminal ||
                !terminal.hasEntityId || !terminal.hasStoreEntityId ||
                !terminal.hasStorageZoneEntityId ||
                !terminal.hasSelectedProductType)
            {
                throw new InvalidOperationException(
                    "Procurement solvency requires a configured terminal.");
            }

            if (_gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                    terminal.EntityId) != null)
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminal.EntityId} cannot evaluate another " +
                    "purchase while a delivery is active.");
            }

            GameEntity store =
                _gameContext.GetEntityWithEntityId(terminal.StoreEntityId);
            GameEntity storageZone =
                _gameContext.GetEntityWithEntityId(terminal.StorageZoneEntityId);
            if (store == null || !store.isStore || !store.hasEntityId ||
                !store.hasMoney || !store.hasNextProjectSequenceIndex ||
                !store.hasProcurementTerminalEntityId ||
                !store.hasStorageZoneEntityId || store.Money < 0 ||
                store.ProcurementTerminalEntityId != terminal.EntityId ||
                store.StorageZoneEntityId != storageZone?.EntityId)
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminal.EntityId} has an invalid store relation.");
            }
            if (storageZone == null || !storageZone.isStorageZone ||
                !storageZone.hasEntityId || !storageZone.hasSlots ||
                !storageZone.hasOccupiedStorageSlotCount ||
                storageZone.OccupiedStorageSlotCount < 0 ||
                storageZone.OccupiedStorageSlotCount > storageZone.Slots.Length)
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminal.EntityId} has an invalid storage relation.");
            }

            ValidateSequenceIndex(store.NextProjectSequenceIndex);
            return new TerminalState(terminal, store, storageZone);
        }

        private TerminalState ValidateAndResolveStore(GameEntity store)
        {
            if (store == null || !store.isStore || !store.hasEntityId ||
                !store.hasMoney || !store.hasNextProjectSequenceIndex ||
                !store.hasProcurementTerminalEntityId ||
                !store.hasStorageZoneEntityId || store.Money < 0)
            {
                throw new InvalidOperationException(
                    "Economy solvency requires a configured store.");
            }

            GameEntity terminal = _gameContext.GetEntityWithEntityId(
                store.ProcurementTerminalEntityId);
            GameEntity storageZone = _gameContext.GetEntityWithEntityId(
                store.StorageZoneEntityId);
            if (terminal == null || !terminal.isProcurementTerminal ||
                !terminal.hasEntityId || !terminal.hasStoreEntityId ||
                !terminal.hasStorageZoneEntityId ||
                terminal.StoreEntityId != store.EntityId ||
                terminal.StorageZoneEntityId != storageZone?.EntityId)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid procurement terminal relation.");
            }
            if (storageZone == null || !storageZone.isStorageZone ||
                !storageZone.hasEntityId || !storageZone.hasSlots ||
                !storageZone.hasOccupiedStorageSlotCount ||
                storageZone.OccupiedStorageSlotCount < 0 ||
                storageZone.OccupiedStorageSlotCount > storageZone.Slots.Length)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid storage relation.");
            }

            ValidateSequenceIndex(store.NextProjectSequenceIndex);
            return new TerminalState(terminal, store, storageZone);
        }

        private void IncludeCommittedDelivery(
            ProjectionState state,
            GameEntity terminal)
        {
            GameEntity delivery =
                _gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                    terminal.EntityId);
            if (delivery == null)
                return;
            if (!delivery.isDelivery || !delivery.isDeliveryActive ||
                !delivery.hasEntityId || !delivery.hasStoreEntityId ||
                !delivery.hasProductType || !delivery.hasDeliveryProductCount ||
                !delivery.hasStockedProductCount ||
                delivery.StoreEntityId != terminal.StoreEntityId ||
                delivery.DeliveryProductCount <= 0 ||
                delivery.StockedProductCount < 0 ||
                delivery.StockedProductCount > delivery.DeliveryProductCount ||
                !state.Stock.ContainsKey(delivery.ProductType))
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminal.EntityId} has an invalid committed " +
                    "delivery.");
            }

            int committedProductCount = checked(
                delivery.DeliveryProductCount - delivery.StockedProductCount);
            state.Stock[delivery.ProductType] = checked(
                state.Stock[delivery.ProductType] + committedProductCount);
            state.OccupiedSlotCount = checked(
                state.OccupiedSlotCount + committedProductCount);
            if (state.OccupiedSlotCount > state.Capacity)
            {
                throw new InvalidOperationException(
                    $"Committed delivery {delivery.EntityId} no longer fits storage " +
                    $"capacity {state.Capacity}.");
            }
        }

        private ProjectionState CreateCurrentState(
            GameEntity store,
            GameEntity storageZone)
        {
            var stock = new Dictionary<ProductTypeId, int>(
                _staticData.ProductTypes.Count);
            foreach (ProductTypeId productType in _staticData.ProductTypes)
                stock.Add(productType, 0);

            int countedStock = 0;
            foreach (GameEntity product in _stockedProducts)
            {
                if (product.StorageZoneEntityId != storageZone.EntityId)
                    continue;
                if (!stock.ContainsKey(product.ProductType))
                {
                    throw new InvalidOperationException(
                        $"Stock product {product.EntityId} has unconfigured type " +
                        $"{product.ProductType}.");
                }

                stock[product.ProductType] = checked(stock[product.ProductType] + 1);
                countedStock = checked(countedStock + 1);
            }

            if (countedStock != storageZone.OccupiedStorageSlotCount)
            {
                throw new InvalidOperationException(
                    $"Storage zone {storageZone.EntityId} reports " +
                    $"{storageZone.OccupiedStorageSlotCount} occupied slots, but " +
                    $"{countedStock} stocked products were found.");
            }

            return new ProjectionState(
                store.Money,
                storageZone.OccupiedStorageSlotCount,
                storageZone.Slots.Length,
                stock);
        }

        private DemandTarget ResolveDemand(GameEntity store)
        {
            GameEntity visit =
                _gameContext.GetEntityWithCustomerVisitStoreEntityId(store.EntityId);
            if (visit == null)
            {
                return CreateForecastDemand(store.NextProjectSequenceIndex);
            }

            ValidateVisit(visit, store);
            int lifecycleCount =
                (visit.isCustomerVisitArriving ? 1 : 0) +
                (visit.isCustomerVisitConsulting ? 1 : 0) +
                (visit.isCustomerVisitLoading ? 1 : 0) +
                (visit.isCustomerVisitCompleted ? 1 : 0) +
                (visit.isCustomerVisitReturning ? 1 : 0) +
                (visit.isCustomerVisitDeparting ? 1 : 0);
            if (lifecycleCount != 1)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} must have exactly one lifecycle state.");
            }

            int visitProjectIndex = GetProjectIndex(visit.CustomerProjectType);
            int followingProjectIndex = NextSequenceIndex(visitProjectIndex);
            if (store.NextProjectSequenceIndex != followingProjectIndex)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} project {visit.CustomerProjectType} " +
                    $"expects next sequence index {followingProjectIndex}, but store " +
                    $"{store.EntityId} exposes {store.NextProjectSequenceIndex}.");
            }

            if (visit.isCustomerVisitLoading)
            {
                if (!visit.isOrder || !visit.hasOrderReward || visit.OrderReward <= 0)
                {
                    throw new InvalidOperationException(
                        $"Active customer visit {visit.EntityId} has incomplete order state.");
                }

                return new DemandTarget(
                    ProcurementDemandKind.ConfirmedOrder,
                    visit.CustomerProjectType,
                    visit,
                    visitProjectIndex);
            }

            if (visit.isCustomerVisitArriving || visit.isCustomerVisitConsulting)
            {
                if (visit.isOrder)
                {
                    throw new InvalidOperationException(
                        $"Unconfirmed customer visit {visit.EntityId} already owns an order.");
                }

                return new DemandTarget(
                    ProcurementDemandKind.ProjectForecast,
                    visit.CustomerProjectType,
                    null,
                    visitProjectIndex);
            }

            return CreateForecastDemand(store.NextProjectSequenceIndex);
        }

        private DemandTarget CreateForecastDemand(int projectSequenceIndex)
        {
            ValidateSequenceIndex(projectSequenceIndex);
            return new DemandTarget(
                ProcurementDemandKind.ProjectForecast,
                _staticData.ProjectTypes[projectSequenceIndex],
                null,
                projectSequenceIndex);
        }

        private bool IsProtectedDemandSolvent(
            ProjectionState afterCandidate,
            DemandTarget demand,
            IReadOnlyDictionary<ProductTypeId, int> exactRequirements)
        {
            int projectCount = _staticData.ProjectTypes.Count;
            if (projectCount <= 0)
                throw new InvalidOperationException("Customer project catalog is empty.");

            try
            {
                if (demand.Kind == ProcurementDemandKind.ProjectForecast)
                {
                    ValidateProjectionBound(demand.ProjectSequenceIndex, projectCount);
                    return AreForecastPathsSolvent(
                        afterCandidate,
                        demand.ProjectSequenceIndex,
                        projectCount);
                }

                if (exactRequirements == null)
                {
                    throw new InvalidOperationException(
                        $"Confirmed order {demand.Order.EntityId} has no projected " +
                        "requirements.");
                }
                if (!TryCompleteRequirements(
                        afterCandidate,
                        exactRequirements,
                        demand.Order.OrderReward,
                        out ProjectionState afterOrder))
                {
                    return false;
                }

                int forecastDepth = projectCount;
                ValidateProjectionBound(
                    NextSequenceIndex(demand.ProjectSequenceIndex),
                    forecastDepth);
                return AreForecastPathsSolvent(
                    afterOrder,
                    NextSequenceIndex(demand.ProjectSequenceIndex),
                    forecastDepth);
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    "Procurement solvency projection must fit a 32-bit signed integer.",
                    exception);
            }
        }

        private bool AreForecastPathsSolvent(
            ProjectionState state,
            int projectSequenceIndex,
            int remainingProjectCount)
        {
            if (remainingProjectCount == 0)
                return true;

            CustomerProjectConfig project = _staticData.GetProject(
                _staticData.ProjectTypes[projectSequenceIndex]);
            if (project.Offers.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Customer project {project.ProjectType} has no offers to project.");
            }

            foreach (CustomerProjectOfferDefinition offer in project.Offers)
            {
                Dictionary<ProductTypeId, int> requirements =
                    CollectOfferRequirements(project, offer);
                int reward = CalculateReward(requirements);
                if (!TryCompleteRequirements(
                        state,
                        requirements,
                        reward,
                        out ProjectionState afterOffer))
                {
                    return false;
                }

                if (!AreForecastPathsSolvent(
                        afterOffer,
                        NextSequenceIndex(projectSequenceIndex),
                        remainingProjectCount - 1))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryCompleteRequirements(
            ProjectionState state,
            IReadOnlyDictionary<ProductTypeId, int> requirements,
            int reward,
            out ProjectionState completed)
        {
            completed = state.Clone();
            int additionalProductCount = 0;
            int additionalCost = 0;
            foreach (ProductTypeId productType in _staticData.ProductTypes)
            {
                requirements.TryGetValue(productType, out int requiredCount);
                if (requiredCount < 0)
                    throw new InvalidOperationException("Projected demand cannot be negative.");

                int availableCount = completed.Stock[productType];
                int missingCount = Math.Max(0, requiredCount - availableCount);
                if (missingCount == 0)
                    continue;

                DeliveryConfig delivery = _staticData.GetDelivery(productType);
                int batchCount = checked(
                    (missingCount + delivery.ProductCount - 1) /
                    delivery.ProductCount);
                int purchasedProductCount = checked(
                    batchCount * delivery.ProductCount);
                additionalProductCount = checked(
                    additionalProductCount + purchasedProductCount);
                additionalCost = checked(
                    additionalCost + checked(batchCount * delivery.TotalCost));
                completed.Stock[productType] = checked(
                    availableCount + purchasedProductCount);
            }

            if (completed.Money < additionalCost ||
                completed.OccupiedSlotCount + additionalProductCount >
                completed.Capacity)
            {
                completed = null;
                return false;
            }

            completed.Money = checked(completed.Money - additionalCost + reward);
            completed.OccupiedSlotCount = checked(
                completed.OccupiedSlotCount + additionalProductCount);
            foreach (KeyValuePair<ProductTypeId, int> requirement in requirements)
            {
                if (!completed.Stock.TryGetValue(
                        requirement.Key,
                        out int availableCount))
                {
                    throw new InvalidOperationException(
                        $"Projected demand references unconfigured product " +
                        $"{requirement.Key}.");
                }
                if (requirement.Value <= 0 || availableCount < requirement.Value)
                {
                    throw new InvalidOperationException(
                        $"Projected demand for {requirement.Key} is invalid.");
                }

                completed.Stock[requirement.Key] = checked(
                    availableCount - requirement.Value);
                completed.OccupiedSlotCount = checked(
                    completed.OccupiedSlotCount - requirement.Value);
            }

            if (completed.Money < 0 || completed.OccupiedSlotCount < 0 ||
                completed.OccupiedSlotCount > completed.Capacity)
            {
                throw new InvalidOperationException(
                    "Procurement projection produced an invalid economy state.");
            }

            return true;
        }

        private Dictionary<ProductTypeId, int> CollectRemainingOrderRequirements(
            GameEntity order,
            IReadOnlyDictionary<ProductTypeId, int> stock)
        {
            GameEntity[] lines = _gameContext
                .GetEntitiesWithOrderEntityId(order.EntityId)
                .OrderBy(line => line.LineIndex)
                .ToArray();
            if (lines.Length == 0 ||
                lines.Length > CustomerProjectConfig.MaxLinesPerOffer)
            {
                throw new InvalidOperationException(
                    $"Order {order.EntityId} has invalid line count {lines.Length}.");
            }

            var requirements = new Dictionary<ProductTypeId, int>(lines.Length);
            var productTypes = new HashSet<ProductTypeId>();
            for (int index = 0; index < lines.Length; index++)
            {
                GameEntity line = lines[index];
                if (!line.isOrderLine || line.isDestructed ||
                    !line.hasEntityId || !line.hasOrderEntityId ||
                    !line.hasStorageZoneEntityId || !line.hasLineIndex ||
                    !line.hasProductType || !line.hasRequiredProductCount ||
                    !line.hasAvailableProductCount ||
                    !line.hasLoadedProductCount ||
                    line.OrderEntityId != order.EntityId ||
                    line.StorageZoneEntityId != order.StorageZoneEntityId ||
                    line.LineIndex != index || line.RequiredProductCount <= 0 ||
                    line.LoadedProductCount < 0 ||
                    line.LoadedProductCount > line.RequiredProductCount ||
                    !stock.TryGetValue(line.ProductType, out int availableCount) ||
                    line.AvailableProductCount != availableCount)
                {
                    throw new InvalidOperationException(
                        $"Order {order.EntityId} has an invalid line at index {index}.");
                }
                if (!productTypes.Add(line.ProductType))
                {
                    throw new InvalidOperationException(
                        $"Order {order.EntityId} contains duplicate product type " +
                        $"{line.ProductType}.");
                }

                int remainingCount = checked(
                    line.RequiredProductCount - line.LoadedProductCount);
                if (remainingCount > 0)
                    requirements.Add(line.ProductType, remainingCount);
            }

            return requirements;
        }

        private Dictionary<ProductTypeId, int> CollectOfferRequirements(
            CustomerProjectConfig project,
            CustomerProjectOfferDefinition offer)
        {
            var requirements = new Dictionary<ProductTypeId, int>(offer.Lines.Count);
            foreach (CustomerProjectLineDefinition line in offer.Lines)
            {
                if (!requirements.TryAdd(line.ProductType, line.RequiredCount))
                {
                    throw new InvalidOperationException(
                        $"Customer project {project.ProjectType} contains duplicate " +
                        $"product type {line.ProductType}.");
                }
            }

            if (requirements.Count == 0 ||
                requirements.Count > CustomerProjectConfig.MaxLinesPerOffer)
            {
                throw new InvalidOperationException(
                    $"Customer project {project.ProjectType} has invalid projected demand.");
            }

            return requirements;
        }

        private int CalculateReward(
            IReadOnlyDictionary<ProductTypeId, int> requirements)
        {
            int reward = 0;
            foreach (KeyValuePair<ProductTypeId, int> requirement in requirements)
            {
                ProductConfig product = _staticData.GetProduct(requirement.Key);
                reward = checked(
                    reward + checked(product.UnitPrice * requirement.Value));
            }

            if (reward <= 0)
                throw new InvalidOperationException("Projected order reward must be positive.");
            return reward;
        }

        private void ValidateProjectionBound(int startSequenceIndex, int projectCount)
        {
            ValidateSequenceIndex(startSequenceIndex);
            if (projectCount < 0 || projectCount > _staticData.ProjectTypes.Count)
                throw new ArgumentOutOfRangeException(nameof(projectCount));

            int leafCount = 1;
            int sequenceIndex = startSequenceIndex;
            for (int offset = 0; offset < projectCount; offset++)
            {
                CustomerProjectConfig project = _staticData.GetProject(
                    _staticData.ProjectTypes[sequenceIndex]);
                leafCount = checked(leafCount * project.Offers.Count);
                if (leafCount > MaximumProjectionLeafCount)
                {
                    throw new InvalidOperationException(
                        $"Procurement solvency projection expands to more than " +
                        $"{MaximumProjectionLeafCount} offer paths.");
                }

                sequenceIndex = NextSequenceIndex(sequenceIndex);
            }
        }

        private void ValidateVisit(GameEntity visit, GameEntity store)
        {
            if (!visit.isCustomerVisit || visit.isDestructed ||
                !visit.hasEntityId || !visit.hasCustomerVisitStoreEntityId ||
                !visit.hasStorageZoneEntityId || !visit.hasCustomerProjectType ||
                visit.CustomerVisitStoreEntityId != store.EntityId ||
                visit.StorageZoneEntityId != store.StorageZoneEntityId)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid customer visit.");
            }
        }

        private int GetProjectIndex(CustomerProjectTypeId projectType)
        {
            for (int index = 0; index < _staticData.ProjectTypes.Count; index++)
            {
                if (_staticData.ProjectTypes[index] == projectType)
                    return index;
            }

            throw new InvalidOperationException(
                $"Customer project {projectType} is missing from deterministic sequence.");
        }

        private void ValidateSequenceIndex(int sequenceIndex)
        {
            if (sequenceIndex < 0 || sequenceIndex >= _staticData.ProjectTypes.Count)
            {
                throw new InvalidOperationException(
                    $"Project sequence index {sequenceIndex} is outside configured " +
                    $"range 0..{_staticData.ProjectTypes.Count - 1}.");
            }
        }

        private int NextSequenceIndex(int sequenceIndex)
        {
            ValidateSequenceIndex(sequenceIndex);
            return (sequenceIndex + 1) % _staticData.ProjectTypes.Count;
        }

        private sealed class ProjectionState
        {
            public ProjectionState(
                int money,
                int occupiedSlotCount,
                int capacity,
                Dictionary<ProductTypeId, int> stock)
            {
                Money = money;
                OccupiedSlotCount = occupiedSlotCount;
                Capacity = capacity;
                Stock = stock;
            }

            public int Money { get; set; }
            public int OccupiedSlotCount { get; set; }
            public int Capacity { get; }
            public Dictionary<ProductTypeId, int> Stock { get; }

            public ProjectionState Clone() => new(
                Money,
                OccupiedSlotCount,
                Capacity,
                new Dictionary<ProductTypeId, int>(Stock));
        }

        private readonly struct TerminalState
        {
            public TerminalState(
                GameEntity terminal,
                GameEntity store,
                GameEntity storageZone)
            {
                Terminal = terminal;
                Store = store;
                StorageZone = storageZone;
            }

            public GameEntity Terminal { get; }
            public GameEntity Store { get; }
            public GameEntity StorageZone { get; }
        }

        private readonly struct DemandTarget
        {
            public DemandTarget(
                ProcurementDemandKind kind,
                CustomerProjectTypeId projectType,
                GameEntity order,
                int projectSequenceIndex)
            {
                Kind = kind;
                ProjectType = projectType;
                Order = order;
                ProjectSequenceIndex = projectSequenceIndex;
            }

            public ProcurementDemandKind Kind { get; }
            public CustomerProjectTypeId ProjectType { get; }
            public GameEntity Order { get; }
            public int ProjectSequenceIndex { get; }
        }
    }
}
