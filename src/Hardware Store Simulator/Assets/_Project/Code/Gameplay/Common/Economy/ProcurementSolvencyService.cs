using System;
using System.Collections.Generic;
using System.Linq;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Common.Economy
{
    public sealed class ProcurementSolvencyService :
        IProcurementSolvencyService,
        IEconomySolvencyService
    {
        private const int MaximumProjectionStateCount = 131072;

        private readonly GameContext _gameContext;
        private readonly IStaticDataService _staticData;
        private readonly IGroup<GameEntity> _stockedProducts;
        private readonly List<CandidateLine> _candidateLines = new(
            ProcurementCartFactory.CurrentDeliveryPackageCapacity);

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
            DeliveryConfig candidateDelivery = _staticData.GetDelivery(productType);
            if (candidateDelivery.ProductType != productType)
            {
                throw new InvalidOperationException(
                    $"Delivery config for {productType} exposes " +
                    $"{candidateDelivery.ProductType}.");
            }

            _candidateLines.Clear();
            _candidateLines.Add(new CandidateLine(
                productType,
                candidateDelivery.ProductCount,
                candidateDelivery.TotalCost));
            return EvaluateCandidate(
                terminalState,
                _candidateLines,
                candidateDelivery.ProductCount,
                candidateDelivery.TotalCost);
        }

        public ProcurementPurchaseEvaluation EvaluateCart(int procurementCartEntityId)
        {
            GameEntity cart = _gameContext.GetEntityWithEntityId(
                procurementCartEntityId);
            if (cart == null || !cart.isProcurementCart || cart.isDestructed ||
                !cart.hasEntityId || !cart.hasProcurementCartTerminalEntityId ||
                !cart.hasStoreEntityId || !cart.hasProcurementCartPackageCapacity ||
                cart.ProcurementCartPackageCapacity <= 0 ||
                cart.ProcurementCartPackageCapacity >
                ProcurementCartFactory.CurrentDeliveryPackageCapacity)
            {
                throw new InvalidOperationException(
                    $"Procurement solvency requires a configured cart " +
                    $"{procurementCartEntityId}.");
            }

            GameEntity terminal = _gameContext.GetEntityWithEntityId(
                cart.ProcurementCartTerminalEntityId);
            TerminalState terminalState = ValidateAndResolveTerminal(terminal);
            if (cart.StoreEntityId != terminalState.Store.EntityId ||
                _gameContext.GetEntityWithProcurementCartTerminalEntityId(
                    terminal.EntityId) != cart)
            {
                throw new InvalidOperationException(
                    $"Procurement cart {cart.EntityId} belongs to another store.");
            }

            _candidateLines.Clear();
            int packageCount = 0;
            int productCount = 0;
            int cost = 0;
            foreach (GameEntity line in _gameContext.GetEntitiesWithProcurementCartEntityId(
                         cart.EntityId))
            {
                if (line.isDestructed)
                    continue;
                if (!line.isProcurementCartLine || !line.hasEntityId ||
                    !line.hasProcurementCartEntityId ||
                    line.ProcurementCartEntityId != cart.EntityId ||
                    !line.hasProductType || !line.hasProcurementPackageCount ||
                    line.ProcurementPackageCount <= 0)
                {
                    throw new InvalidOperationException(
                        $"Procurement cart {cart.EntityId} contains an invalid line.");
                }

                for (int index = 0; index < _candidateLines.Count; index++)
                {
                    if (_candidateLines[index].ProductType == line.ProductType)
                    {
                        throw new InvalidOperationException(
                            $"Procurement cart {cart.EntityId} contains duplicate " +
                            $"{line.ProductType} lines.");
                    }
                }

                DeliveryConfig config = _staticData.GetDelivery(line.ProductType);
                if (config.ProductType != line.ProductType)
                {
                    throw new InvalidOperationException(
                        $"Delivery config for {line.ProductType} exposes " +
                        $"{config.ProductType}.");
                }
                int lineProductCount = checked(
                    line.ProcurementPackageCount * config.ProductCount);
                int lineCost = checked(
                    line.ProcurementPackageCount * config.TotalCost);
                packageCount = checked(
                    packageCount + line.ProcurementPackageCount);
                productCount = checked(productCount + lineProductCount);
                cost = checked(cost + lineCost);
                _candidateLines.Add(new CandidateLine(
                    line.ProductType,
                    lineProductCount,
                    lineCost));
            }
            if (_candidateLines.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Procurement cart {cart.EntityId} is empty.");
            }
            if (packageCount > cart.ProcurementCartPackageCapacity)
            {
                throw new InvalidOperationException(
                    $"Procurement cart {cart.EntityId} contains {packageCount} packages; " +
                    $"capacity is {cart.ProcurementCartPackageCapacity}.");
            }

            return EvaluateCandidate(
                terminalState,
                _candidateLines,
                productCount,
                cost);
        }

        private ProcurementPurchaseEvaluation EvaluateCandidate(
            TerminalState terminalState,
            IReadOnlyList<CandidateLine> candidateLines,
            int candidateProductCount,
            int candidateCost)
        {
            if (candidateLines.Count <= 0 || candidateProductCount <= 0 ||
                candidateCost <= 0)
            {
                throw new InvalidOperationException(
                    "Procurement purchase candidate is invalid.");
            }
            int validatedProductCount = 0;
            int validatedCost = 0;
            for (int index = 0; index < candidateLines.Count; index++)
            {
                CandidateLine line = candidateLines[index];
                for (int previousIndex = 0; previousIndex < index; previousIndex++)
                {
                    if (candidateLines[previousIndex].ProductType == line.ProductType)
                    {
                        throw new InvalidOperationException(
                            $"Procurement purchase candidate contains duplicate " +
                            $"{line.ProductType} lines.");
                    }
                }
                validatedProductCount = checked(
                    validatedProductCount + line.ProductCount);
                validatedCost = checked(validatedCost + line.Cost);
            }
            if (validatedProductCount != candidateProductCount ||
                validatedCost != candidateCost)
            {
                throw new InvalidOperationException(
                    "Procurement purchase candidate aggregate totals are invalid.");
            }

            ProjectionState state = CreateCurrentState(
                terminalState.Store,
                terminalState.StorageZone);
            DemandPlan demandPlan = ResolveDemandPlan(
                terminalState.Store,
                state.Stock);
            int moneyAfterPurchase;
            try
            {
                moneyAfterPurchase = checked(state.Money - candidateCost);
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    "Procurement purchase totals must fit a 32-bit signed integer.",
                    exception);
            }

            ProcurementPurchaseAvailability availability;
            if (state.Capacity - state.OccupiedSlotCount <
                candidateProductCount)
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
                    candidateProductCount);
                for (int index = 0; index < candidateLines.Count; index++)
                {
                    CandidateLine line = candidateLines[index];
                    if (!afterCandidate.Stock.TryGetValue(
                            line.ProductType,
                            out int currentCount))
                    {
                        throw new InvalidOperationException(
                            $"Purchase candidate references unconfigured product " +
                            $"{line.ProductType}.");
                    }
                    afterCandidate.Stock[line.ProductType] = checked(
                        currentCount + line.ProductCount);
                }

                availability = IsProtectedDemandSolvent(
                        afterCandidate,
                        demandPlan)
                    ? ProcurementPurchaseAvailability.Available
                    : ProcurementPurchaseAvailability.DemandWouldBecomeInsolvent;
            }

            return new ProcurementPurchaseEvaluation(
                availability,
                demandPlan.Summary.Kind,
                demandPlan.Summary.ProjectType,
                demandPlan.Summary.VisitEntityId,
                candidateProductCount,
                candidateCost,
                moneyAfterPurchase);
        }

        public EconomyDebitEvaluation EvaluateDebit(int storeEntityId, int debitAmount)
        {
            if (debitAmount <= 0)
                throw new ArgumentOutOfRangeException(nameof(debitAmount));

            GameEntity store = _gameContext.GetEntityWithEntityId(storeEntityId);
            TerminalState terminalState = ValidateAndResolveStore(store);
            ProjectionState state = CreateCurrentState(
                store,
                terminalState.StorageZone);
            DemandPlan demandPlan = ResolveDemandPlan(store, state.Stock);
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
                        demandPlan)
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
            if (_gameContext.GetEntityWithPurchaseOrderProcurementTerminalEntityId(
                    terminal.EntityId) != null)
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminal.EntityId} cannot evaluate another " +
                    "purchase while a purchase order is active.");
            }

            GameEntity store =
                _gameContext.GetEntityWithEntityId(terminal.StoreEntityId);
            GameEntity storageZone =
                _gameContext.GetEntityWithEntityId(terminal.StorageZoneEntityId);
            if (store == null || !store.isStore || !store.hasEntityId ||
                !store.hasMoney || !store.hasNextProjectSequenceIndex ||
                !store.hasNextCustomerArrivalSequence ||
                !store.hasProcurementTerminalEntityId ||
                !store.hasStorageZoneEntityId || store.Money < 0 ||
                store.NextCustomerArrivalSequence < 0 ||
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
                !store.hasNextCustomerArrivalSequence ||
                !store.hasProcurementTerminalEntityId ||
                !store.hasStorageZoneEntityId || store.Money < 0 ||
                store.NextCustomerArrivalSequence < 0)
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
                !delivery.hasDeliveryPurchaseOrderEntityId ||
                !delivery.hasDeliveryProductCount || !delivery.hasStockedProductCount ||
                delivery.StoreEntityId != terminal.StoreEntityId ||
                delivery.DeliveryProductCount <= 0 ||
                delivery.StockedProductCount < 0 ||
                delivery.StockedProductCount > delivery.DeliveryProductCount)
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminal.EntityId} has an invalid committed " +
                    "delivery.");
            }

            GameEntity order = _gameContext.GetEntityWithEntityId(
                delivery.DeliveryPurchaseOrderEntityId);
            if (order == null || !order.isPurchaseOrder || order.isDestructed ||
                !order.hasEntityId || !order.hasStoreEntityId ||
                order.StoreEntityId != terminal.StoreEntityId ||
                !order.hasPurchaseOrderProcurementTerminalEntityId ||
                order.PurchaseOrderProcurementTerminalEntityId != terminal.EntityId ||
                !order.hasPurchaseOrderProductCount ||
                order.PurchaseOrderProductCount != delivery.DeliveryProductCount)
            {
                throw new InvalidOperationException(
                    $"Committed delivery {delivery.EntityId} has an invalid purchase order.");
            }

            int committedProductCount = 0;
            int registeredProductCount = 0;
            foreach (GameEntity line in _gameContext.GetEntitiesWithPurchaseOrderEntityId(
                         order.EntityId))
            {
                if (!line.isPurchaseOrderLine || line.isDestructed ||
                    !line.hasEntityId || !line.hasProductType ||
                    !line.hasPurchaseOrderLineProductCount ||
                    !line.hasPurchaseOrderLineStockedProductCount ||
                    line.PurchaseOrderLineProductCount <= 0 ||
                    line.PurchaseOrderLineStockedProductCount < 0 ||
                    line.PurchaseOrderLineStockedProductCount >
                    line.PurchaseOrderLineProductCount ||
                    !state.Stock.ContainsKey(line.ProductType))
                {
                    throw new InvalidOperationException(
                        $"Purchase order {order.EntityId} contains an invalid committed line.");
                }

                int lineCommittedCount = checked(
                    line.PurchaseOrderLineProductCount -
                    line.PurchaseOrderLineStockedProductCount);
                state.Stock[line.ProductType] = checked(
                    state.Stock[line.ProductType] + lineCommittedCount);
                committedProductCount = checked(
                    committedProductCount + lineCommittedCount);
                registeredProductCount = checked(
                    registeredProductCount +
                    line.PurchaseOrderLineStockedProductCount);
            }
            if (committedProductCount != checked(
                    delivery.DeliveryProductCount - delivery.StockedProductCount) ||
                registeredProductCount != delivery.StockedProductCount)
            {
                throw new InvalidOperationException(
                    $"Committed delivery {delivery.EntityId} aggregate counters disagree " +
                    "with its manifest.");
            }

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

        private DemandPlan ResolveDemandPlan(
            GameEntity store,
            IReadOnlyDictionary<ProductTypeId, int> stock)
        {
            ValidateSequenceIndex(store.NextProjectSequenceIndex);
            GameEntity[] visits = _gameContext.GetEntitiesWithCustomerVisitStoreEntityId(
                    store.EntityId)
                .ToArray();
            foreach (GameEntity visit in visits)
            {
                ValidateVisit(visit, store);
                ValidateVisitLifecycle(visit);
                if (visit.CustomerArrivalSequence < 0 ||
                    visit.CustomerArrivalSequence >= store.NextCustomerArrivalSequence)
                {
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has arrival sequence " +
                        $"{visit.CustomerArrivalSequence} outside store {store.EntityId} " +
                        $"counter {store.NextCustomerArrivalSequence}.");
                }
            }

            visits = visits
                .OrderBy(visit => visit.CustomerArrivalSequence)
                .ThenBy(visit => visit.EntityId)
                .ToArray();
            var protectedDemands = new List<ProtectedDemand>(visits.Length);
            int previousArrivalSequence = -1;
            int previousProjectIndex = -1;
            foreach (GameEntity visit in visits)
            {
                if (visit.CustomerArrivalSequence == previousArrivalSequence)
                {
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} has invalid or duplicate customer arrival " +
                        $"sequence {visit.CustomerArrivalSequence}.");
                }

                int projectIndex = GetProjectIndex(visit.CustomerProjectType);
                if (previousProjectIndex >= 0)
                {
                    int arrivalSequenceDelta =
                        visit.CustomerArrivalSequence - previousArrivalSequence;
                    if (arrivalSequenceDelta <= 0)
                    {
                        throw new InvalidOperationException(
                            $"Store {store.EntityId} customer arrival sequences must be " +
                            "strictly increasing.");
                    }

                    int expectedProjectIndex = AdvanceSequenceIndex(
                        previousProjectIndex,
                        arrivalSequenceDelta);
                    if (projectIndex != expectedProjectIndex)
                    {
                        throw new InvalidOperationException(
                            $"Customer visit {visit.EntityId} project " +
                            $"{visit.CustomerProjectType} breaks the store " +
                            $"{store.EntityId} arrival sequence across a gap of " +
                            $"{arrivalSequenceDelta} visits.");
                    }
                }

                previousArrivalSequence = visit.CustomerArrivalSequence;
                previousProjectIndex = projectIndex;
                if (IsAbandonedPreOrderVisit(visit))
                {
                    ValidateAbandonedPreOrderVisit(visit);
                    continue;
                }
                if (visit.isOrderRewarded)
                {
                    if (!visit.isOrder ||
                        (!visit.isCustomerVisitCompleted &&
                         !visit.isCustomerVisitDeparting))
                    {
                        throw new InvalidOperationException(
                            $"Rewarded customer visit {visit.EntityId} has an invalid " +
                            "protected-demand lifecycle.");
                    }

                    continue;
                }

                if (visit.isOrder)
                {
                    if (!visit.hasOrderReward || visit.OrderReward <= 0)
                    {
                        throw new InvalidOperationException(
                            $"Confirmed customer visit {visit.EntityId} has no positive reward.");
                    }

                    protectedDemands.Add(ProtectedDemand.ConfirmedOrder(
                        visit,
                        CollectRemainingOrderRequirements(visit, stock)));
                    continue;
                }

                if (!visit.isCustomerVisitArriving &&
                    !visit.isCustomerVisitQueued &&
                    !visit.isCustomerVisitConsulting)
                {
                    throw new InvalidOperationException(
                        $"Pre-order customer visit {visit.EntityId} has invalid lifecycle.");
                }

                protectedDemands.Add(ProtectedDemand.ProjectForecast(visit));
            }

            if (previousProjectIndex >= 0)
            {
                int remainingArrivalSequenceCount =
                    store.NextCustomerArrivalSequence - previousArrivalSequence;
                if (remainingArrivalSequenceCount <= 0)
                {
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} next customer arrival sequence must follow " +
                        "its latest active visit.");
                }

                int expectedNextProjectIndex = AdvanceSequenceIndex(
                    previousProjectIndex,
                    remainingArrivalSequenceCount);
                if (store.NextProjectSequenceIndex != expectedNextProjectIndex)
                {
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} next project sequence index " +
                        $"{store.NextProjectSequenceIndex} does not account for all " +
                        $"{remainingArrivalSequenceCount} arrivals since its latest " +
                        "active customer visit.");
                }
            }

            DemandSummary summary = protectedDemands.Count > 0
                ? protectedDemands[0].CreateSummary()
                : new DemandSummary(
                    ProcurementDemandKind.ProjectForecast,
                    _staticData.ProjectTypes[store.NextProjectSequenceIndex],
                    null);
            var demandPlan = new DemandPlan(
                protectedDemands,
                summary,
                store.NextProjectSequenceIndex,
                _staticData.ProjectTypes.Count);
            ValidateProjectionPlan(demandPlan);
            return demandPlan;
        }

        private bool IsProtectedDemandSolvent(
            ProjectionState afterCandidate,
            DemandPlan demandPlan)
        {
            if (demandPlan.FutureProjectCount <= 0)
                throw new InvalidOperationException("Customer project catalog is empty.");

            try
            {
                var memo = new ProjectionMemo(
                    _staticData.ProductTypes,
                    MaximumProjectionStateCount);
                return AreProtectedDemandsSolvent(
                    afterCandidate,
                    demandPlan,
                    demandIndex: 0,
                    memo);
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    "Procurement solvency projection must fit a 32-bit signed integer.",
                    exception);
            }
        }

        private bool AreProtectedDemandsSolvent(
            ProjectionState state,
            DemandPlan demandPlan,
            int demandIndex,
            ProjectionMemo memo)
        {
            ProjectionKey key = memo.CreateKey(
                ProjectionPhase.ProtectedDemand,
                demandIndex,
                secondaryStage: 0,
                state);
            if (memo.TryGetCompleted(key, out bool cachedResult))
                return cachedResult;
            memo.Register(key);

            bool result;
            if (demandIndex == demandPlan.ProtectedDemands.Count)
            {
                result = AreForecastPathsSolvent(
                    state,
                    demandPlan.FutureProjectSequenceIndex,
                    demandPlan.FutureProjectCount,
                    memo);
                memo.Complete(key, result);
                return result;
            }

            ProtectedDemand demand = demandPlan.ProtectedDemands[demandIndex];
            if (demand.Kind == ProcurementDemandKind.ConfirmedOrder)
            {
                if (!TryCompleteRequirements(
                        state,
                        demand.ExactRequirements,
                        demand.Reward,
                        out ProjectionState afterOrder))
                {
                    memo.Complete(key, result: false);
                    return false;
                }

                result = AreProtectedDemandsSolvent(
                    afterOrder,
                    demandPlan,
                    demandIndex + 1,
                    memo);
                memo.Complete(key, result);
                return result;
            }

            CustomerProjectConfig project = _staticData.GetProject(demand.ProjectType);
            if (project.Offers.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Customer project {project.ProjectType} has no offers to project.");
            }

            result = true;
            foreach (CustomerProjectOfferDefinition offer in project.Offers)
            {
                Dictionary<ProductTypeId, int> requirements =
                    CollectOfferRequirements(project, offer);
                int reward = CalculateReward(requirements);
                if (!TryCompleteRequirements(
                        state,
                        requirements,
                        reward,
                        out ProjectionState afterOffer) ||
                    !AreProtectedDemandsSolvent(
                        afterOffer,
                        demandPlan,
                        demandIndex + 1,
                        memo))
                {
                    result = false;
                    break;
                }
            }

            memo.Complete(key, result);
            return result;
        }

        private bool AreForecastPathsSolvent(
            ProjectionState state,
            int projectSequenceIndex,
            int remainingProjectCount,
            ProjectionMemo memo)
        {
            ProjectionKey key = memo.CreateKey(
                ProjectionPhase.Forecast,
                projectSequenceIndex,
                remainingProjectCount,
                state);
            if (memo.TryGetCompleted(key, out bool cachedResult))
                return cachedResult;
            memo.Register(key);

            if (remainingProjectCount == 0)
            {
                memo.Complete(key, result: true);
                return true;
            }

            CustomerProjectConfig project = _staticData.GetProject(
                _staticData.ProjectTypes[projectSequenceIndex]);
            if (project.Offers.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Customer project {project.ProjectType} has no offers to project.");
            }

            bool result = true;
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
                    result = false;
                    break;
                }

                if (!AreForecastPathsSolvent(
                        afterOffer,
                        NextSequenceIndex(projectSequenceIndex),
                        remainingProjectCount - 1,
                        memo))
                {
                    result = false;
                    break;
                }
            }

            memo.Complete(key, result);
            return result;
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
            HashSet<GameEntity> indexedLines =
                _gameContext.GetEntitiesWithOrderEntityId(order.EntityId);
            foreach (GameEntity line in indexedLines)
            {
                if (!line.hasLineIndex)
                {
                    throw new InvalidOperationException(
                        $"Order {order.EntityId} contains a line without an index.");
                }
            }

            GameEntity[] lines = indexedLines
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

        private void ValidateProjectionPlan(DemandPlan demandPlan)
        {
            ValidateSequenceIndex(demandPlan.FutureProjectSequenceIndex);
            if (demandPlan.FutureProjectCount < 0 ||
                demandPlan.FutureProjectCount > _staticData.ProjectTypes.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(demandPlan.FutureProjectCount));
            }

            for (int index = 0; index < demandPlan.ProtectedDemands.Count; index++)
            {
                ProtectedDemand demand = demandPlan.ProtectedDemands[index];
                if (demand.Kind != ProcurementDemandKind.ProjectForecast)
                    continue;

                CustomerProjectConfig project = _staticData.GetProject(demand.ProjectType);
                ValidateProjectionProject(project);
            }

            int sequenceIndex = demandPlan.FutureProjectSequenceIndex;
            for (int offset = 0; offset < demandPlan.FutureProjectCount; offset++)
            {
                CustomerProjectConfig project = _staticData.GetProject(
                    _staticData.ProjectTypes[sequenceIndex]);
                ValidateProjectionProject(project);
                sequenceIndex = NextSequenceIndex(sequenceIndex);
            }
        }

        private static void ValidateProjectionProject(CustomerProjectConfig project)
        {
            int offerCount = project.Offers.Count;
            if (offerCount <= 0)
            {
                throw new InvalidOperationException(
                    $"Customer project {project.ProjectType} has no offers to project.");
            }
        }

        private void ValidateVisit(GameEntity visit, GameEntity store)
        {
            if (!visit.isCustomerVisit || visit.isDestructed ||
                !visit.hasEntityId || !visit.hasCustomerVisitStoreEntityId ||
                !visit.hasStorageZoneEntityId || !visit.hasCustomerProjectType ||
                !visit.hasCustomerArrivalSequence ||
                visit.CustomerVisitStoreEntityId != store.EntityId ||
                visit.StorageZoneEntityId != store.StorageZoneEntityId)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid customer visit.");
            }
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

        private static bool IsAbandonedPreOrderVisit(GameEntity visit) =>
            visit.isCustomerVisitAbandoning ||
            visit.isCustomerVisitWaitingForAbandonDeparture ||
            visit.isCustomerVisitAbandonDeparting;

        private void ValidateAbandonedPreOrderVisit(GameEntity visit)
        {
            if (visit.isOrder || visit.isOrderRewarded ||
                visit.isOrderContentReleased || visit.hasOrderReward ||
                visit.hasCustomerPatienceRemaining ||
                visit.isCustomerPatienceWarningIssued ||
                visit.hasServingOrderCounterEntityId ||
                visit.hasReservedCustomerLoadingBayEntityId ||
                _gameContext.GetEntitiesWithConsultationOfferVisitEntityId(
                    visit.EntityId).Count != 0 ||
                _gameContext.GetEntitiesWithOrderEntityId(visit.EntityId).Count != 0)
            {
                throw new InvalidOperationException(
                    $"Abandoned customer visit {visit.EntityId} retains protected-demand " +
                    "state.");
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
            => AdvanceSequenceIndex(sequenceIndex, 1);

        private int AdvanceSequenceIndex(int sequenceIndex, int stepCount)
        {
            ValidateSequenceIndex(sequenceIndex);
            if (stepCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(stepCount));

            int boundedStepCount = stepCount % _staticData.ProjectTypes.Count;
            return (int)(((long)sequenceIndex + boundedStepCount) %
                         _staticData.ProjectTypes.Count);
        }

        private enum ProjectionPhase
        {
            ProtectedDemand = 0,
            Forecast = 1
        }

        private readonly struct CandidateLine
        {
            public CandidateLine(ProductTypeId productType, int productCount, int cost)
            {
                if (productCount <= 0)
                    throw new ArgumentOutOfRangeException(nameof(productCount));
                if (cost <= 0)
                    throw new ArgumentOutOfRangeException(nameof(cost));

                ProductType = productType;
                ProductCount = productCount;
                Cost = cost;
            }

            public ProductTypeId ProductType { get; }
            public int ProductCount { get; }
            public int Cost { get; }
        }

        private sealed class ProjectionMemo
        {
            private const byte Pending = 0;
            private const byte CompletedFalse = 1;
            private const byte CompletedTrue = 2;

            private readonly IReadOnlyList<ProductTypeId> _productTypes;
            private readonly int _maximumStateCount;
            private readonly Dictionary<ProjectionKey, byte> _results = new();

            public ProjectionMemo(IReadOnlyList<ProductTypeId> productTypes,
                int maximumStateCount)
            {
                if (productTypes == null || productTypes.Count == 0)
                    throw new ArgumentException("Projection product catalog is empty.",
                        nameof(productTypes));
                if (maximumStateCount <= 0)
                    throw new ArgumentOutOfRangeException(nameof(maximumStateCount));

                _productTypes = productTypes;
                _maximumStateCount = maximumStateCount;
            }

            public ProjectionKey CreateKey(ProjectionPhase phase, int primaryStage,
                int secondaryStage, ProjectionState state)
            {
                var stock = new int[_productTypes.Count];
                for (int index = 0; index < _productTypes.Count; index++)
                {
                    ProductTypeId productType = _productTypes[index];
                    if (!state.Stock.TryGetValue(productType, out int count) || count < 0)
                    {
                        throw new InvalidOperationException(
                            $"Projection state has invalid stock for {productType}.");
                    }
                    stock[index] = count;
                }

                return new ProjectionKey(
                    phase,
                    primaryStage,
                    secondaryStage,
                    state.Money,
                    state.OccupiedSlotCount,
                    state.Capacity,
                    stock);
            }

            public bool TryGetCompleted(ProjectionKey key, out bool result)
            {
                if (!_results.TryGetValue(key, out byte value))
                {
                    result = false;
                    return false;
                }
                if (value == Pending)
                {
                    throw new InvalidOperationException(
                        "Procurement projection unexpectedly reached a cyclic state.");
                }

                result = value == CompletedTrue;
                return true;
            }

            public void Register(ProjectionKey key)
            {
                if (_results.Count >= _maximumStateCount)
                {
                    throw new InvalidOperationException(
                        $"Procurement solvency projection explored " +
                        $"{_results.Count} unique states and reached its strict " +
                        $"{_maximumStateCount}-state limit.");
                }
                if (!_results.TryAdd(key, Pending))
                    throw new InvalidOperationException("Projection state was registered twice.");
            }

            public void Complete(ProjectionKey key, bool result)
            {
                if (!_results.TryGetValue(key, out byte value) || value != Pending)
                {
                    throw new InvalidOperationException(
                        "Projection state completion does not match its registration.");
                }
                _results[key] = result ? CompletedTrue : CompletedFalse;
            }
        }

        private sealed class ProjectionKey : IEquatable<ProjectionKey>
        {
            private readonly int[] _stock;
            private readonly int _hashCode;

            public ProjectionKey(ProjectionPhase phase, int primaryStage,
                int secondaryStage, int money, int occupiedSlotCount, int capacity,
                int[] stock)
            {
                Phase = phase;
                PrimaryStage = primaryStage;
                SecondaryStage = secondaryStage;
                Money = money;
                OccupiedSlotCount = occupiedSlotCount;
                Capacity = capacity;
                _stock = stock ?? throw new ArgumentNullException(nameof(stock));

                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + (int)phase;
                    hash = hash * 31 + primaryStage;
                    hash = hash * 31 + secondaryStage;
                    hash = hash * 31 + money;
                    hash = hash * 31 + occupiedSlotCount;
                    hash = hash * 31 + capacity;
                    for (int index = 0; index < _stock.Length; index++)
                        hash = hash * 31 + _stock[index];
                    _hashCode = hash;
                }
            }

            private ProjectionPhase Phase { get; }
            private int PrimaryStage { get; }
            private int SecondaryStage { get; }
            private int Money { get; }
            private int OccupiedSlotCount { get; }
            private int Capacity { get; }

            public bool Equals(ProjectionKey other)
            {
                if (ReferenceEquals(this, other))
                    return true;
                if (other == null || Phase != other.Phase ||
                    PrimaryStage != other.PrimaryStage ||
                    SecondaryStage != other.SecondaryStage ||
                    Money != other.Money ||
                    OccupiedSlotCount != other.OccupiedSlotCount ||
                    Capacity != other.Capacity ||
                    _stock.Length != other._stock.Length)
                {
                    return false;
                }

                for (int index = 0; index < _stock.Length; index++)
                {
                    if (_stock[index] != other._stock[index])
                        return false;
                }
                return true;
            }

            public override bool Equals(object obj) => Equals(obj as ProjectionKey);
            public override int GetHashCode() => _hashCode;
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

        private sealed class DemandPlan
        {
            public DemandPlan(
                IReadOnlyList<ProtectedDemand> protectedDemands,
                DemandSummary summary,
                int futureProjectSequenceIndex,
                int futureProjectCount)
            {
                ProtectedDemands = protectedDemands;
                Summary = summary;
                FutureProjectSequenceIndex = futureProjectSequenceIndex;
                FutureProjectCount = futureProjectCount;
            }

            public IReadOnlyList<ProtectedDemand> ProtectedDemands { get; }
            public DemandSummary Summary { get; }
            public int FutureProjectSequenceIndex { get; }
            public int FutureProjectCount { get; }
        }

        private readonly struct ProtectedDemand
        {
            private ProtectedDemand(
                ProcurementDemandKind kind,
                CustomerProjectTypeId projectType,
                int visitEntityId,
                IReadOnlyDictionary<ProductTypeId, int> exactRequirements,
                int reward)
            {
                Kind = kind;
                ProjectType = projectType;
                VisitEntityId = visitEntityId;
                ExactRequirements = exactRequirements;
                Reward = reward;
            }

            public ProcurementDemandKind Kind { get; }
            public CustomerProjectTypeId ProjectType { get; }
            public int VisitEntityId { get; }
            public IReadOnlyDictionary<ProductTypeId, int> ExactRequirements { get; }
            public int Reward { get; }

            public static ProtectedDemand ConfirmedOrder(
                GameEntity visit,
                IReadOnlyDictionary<ProductTypeId, int> exactRequirements) =>
                new(
                    ProcurementDemandKind.ConfirmedOrder,
                    visit.CustomerProjectType,
                    visit.EntityId,
                    exactRequirements,
                    visit.OrderReward);

            public static ProtectedDemand ProjectForecast(GameEntity visit) =>
                new(
                    ProcurementDemandKind.ProjectForecast,
                    visit.CustomerProjectType,
                    visit.EntityId,
                    null,
                    0);

            public DemandSummary CreateSummary() =>
                new(Kind, ProjectType, VisitEntityId);
        }

        private readonly struct DemandSummary
        {
            public DemandSummary(
                ProcurementDemandKind kind,
                CustomerProjectTypeId projectType,
                int? visitEntityId)
            {
                Kind = kind;
                ProjectType = projectType;
                VisitEntityId = visitEntityId;
            }

            public ProcurementDemandKind Kind { get; }
            public CustomerProjectTypeId ProjectType { get; }
            public int? VisitEntityId { get; }
        }
    }
}
