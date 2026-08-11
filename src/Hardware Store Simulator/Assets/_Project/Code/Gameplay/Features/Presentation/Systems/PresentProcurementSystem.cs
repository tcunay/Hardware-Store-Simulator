using System;
using System.Linq;
using Entitas;
using HardwareStore.Gameplay.Common.Economy;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Presentation;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Presentation.Systems
{
    public sealed class PresentProcurementSystem : IExecuteSystem
    {
        private const int ProductCardCount = 2;

        private readonly GameContext _gameContext;
        private readonly IStaticDataService _staticData;
        private readonly IProcurementSolvencyService _solvency;
        private readonly IHudService _hud;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<GameEntity> _stockedProducts;

        public PresentProcurementSystem(GameContext gameContext,
            IStaticDataService staticData,
            IProcurementSolvencyService solvency,
            IHudService hud)
        {
            _gameContext = gameContext;
            _staticData = staticData;
            _solvency = solvency;
            _hud = hud;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.ModalOpen,
                GameMatcher.ProcurementTerminalEntityId));
            _stockedProducts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.InStock,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.ProductType)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            _hud.PresentProcurement(null);

            GameEntity[] players = _players.GetEntities();
            if (players.Length > 1)
            {
                throw new InvalidOperationException(
                    "The local HUD cannot present more than one procurement catalog at a time.");
            }
            if (players.Length == 0)
                return;

            GameEntity player = players[0];
            if (player.hasConsultationVisitEntityId)
            {
                throw new InvalidOperationException(
                    "Consultation and procurement modals cannot be open at the same time.");
            }

            GameEntity terminal = _gameContext.GetEntityWithEntityId(
                player.ProcurementTerminalEntityId);
            ValidateTerminal(player, terminal);

            GameEntity store = _gameContext.GetEntityWithEntityId(terminal.StoreEntityId);
            GameEntity storageZone = _gameContext.GetEntityWithEntityId(
                terminal.StorageZoneEntityId);
            ValidateStoreAndStorage(terminal, store, storageZone);

            if (_gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                    terminal.EntityId) != null)
            {
                throw new InvalidOperationException(
                    $"Procurement catalog for terminal {terminal.EntityId} cannot remain open " +
                    "while a delivery is active.");
            }

            GameEntity visit = _gameContext.GetEntityWithCustomerVisitStoreEntityId(
                store.EntityId);
            ProductTypeId[] productTypes = _staticData.ProductTypes.ToArray();
            if (productTypes.Length != ProductCardCount)
            {
                throw new InvalidOperationException(
                    $"The prototype procurement catalog requires exactly {ProductCardCount} " +
                    $"product types, found {productTypes.Length}.");
            }

            var evaluations = new ProcurementPurchaseEvaluation[productTypes.Length];
            for (int index = 0; index < productTypes.Length; index++)
            {
                evaluations[index] = _solvency.EvaluatePurchase(
                    terminal.EntityId,
                    productTypes[index]);
            }
            ProcurementDemandKind demandKind = evaluations[0].DemandKind;
            CustomerProjectTypeId projectType = evaluations[0].ProjectType;
            for (int index = 1; index < evaluations.Length; index++)
            {
                if (evaluations[index].DemandKind != demandKind ||
                    evaluations[index].ProjectType != projectType)
                {
                    throw new InvalidOperationException(
                        "One procurement catalog cannot mix different demand plans.");
                }
            }

            GameEntity[] orderLines = demandKind == ProcurementDemandKind.ConfirmedOrder
                ? GetConfirmedOrderLines(visit, store, storageZone)
                : Array.Empty<GameEntity>();
            CustomerProjectConfig project = _staticData.GetProject(projectType);
            int freeStorageSlotCount =
                storageZone.Slots.Length - storageZone.OccupiedStorageSlotCount;
            var products = new ProcurementProductSnapshot[productTypes.Length];
            for (int index = 0; index < productTypes.Length; index++)
            {
                products[index] = CreateProductSnapshot(
                    index,
                    productTypes[index],
                    terminal,
                    storageZone,
                    evaluations[index],
                    project,
                    orderLines);
            }

            _hud.PresentProcurement(new ProcurementSnapshot(
                demandKind,
                projectType,
                store.Money,
                freeStorageSlotCount,
                products));
        }

        private ProcurementProductSnapshot CreateProductSnapshot(
            int index,
            ProductTypeId productType,
            GameEntity terminal,
            GameEntity storageZone,
            ProcurementPurchaseEvaluation evaluation,
            CustomerProjectConfig project,
            GameEntity[] orderLines)
        {
            GameEntity orderLine = null;
            for (int lineIndex = 0; lineIndex < orderLines.Length; lineIndex++)
            {
                if (orderLines[lineIndex].ProductType != productType)
                    continue;

                orderLine = orderLines[lineIndex];
                break;
            }

            int availableProductCount = CountAvailableProducts(
                storageZone.EntityId,
                productType);
            if (orderLine != null &&
                orderLine.AvailableProductCount != availableProductCount)
            {
                throw new InvalidOperationException(
                    $"Order line {orderLine.EntityId} has stale product availability.");
            }
            int remainingRequiredProductCount = orderLine == null
                ? 0
                : orderLine.RequiredProductCount - orderLine.LoadedProductCount;
            int deficitProductCount = Math.Max(
                0,
                remainingRequiredProductCount - availableProductCount);
            DeliveryConfig delivery = _staticData.GetDelivery(productType);
            if (evaluation.DeliveryProductCount != delivery.ProductCount ||
                evaluation.DeliveryCost != delivery.TotalCost)
            {
                throw new InvalidOperationException(
                    $"Procurement evaluation for {productType} disagrees with static data.");
            }
            ResolveDemandRange(
                evaluation.DemandKind,
                project,
                productType,
                remainingRequiredProductCount,
                out int minimumRequiredProductCount,
                out int maximumRequiredProductCount);

            return new ProcurementProductSnapshot(
                index,
                productType,
                delivery.ProductCount,
                delivery.TotalCost,
                evaluation.MoneyAfterPurchase,
                availableProductCount,
                minimumRequiredProductCount,
                maximumRequiredProductCount,
                remainingRequiredProductCount,
                deficitProductCount,
                MapPurchaseState(evaluation.Availability),
                terminal.SelectedProductType == productType);
        }

        private int CountAvailableProducts(int storageZoneEntityId, ProductTypeId productType)
        {
            int count = 0;
            foreach (GameEntity product in _stockedProducts)
            {
                if (product.StorageZoneEntityId == storageZoneEntityId &&
                    product.ProductType == productType)
                {
                    count++;
                }
            }

            return count;
        }

        private static void ResolveDemandRange(
            ProcurementDemandKind demandKind,
            CustomerProjectConfig project,
            ProductTypeId productType,
            int remainingRequiredProductCount,
            out int minimumRequiredProductCount,
            out int maximumRequiredProductCount)
        {
            if (demandKind == ProcurementDemandKind.ConfirmedOrder)
            {
                minimumRequiredProductCount = remainingRequiredProductCount;
                maximumRequiredProductCount = remainingRequiredProductCount;
                return;
            }
            if (demandKind != ProcurementDemandKind.ProjectForecast)
                throw new ArgumentOutOfRangeException(nameof(demandKind), demandKind, null);

            minimumRequiredProductCount = int.MaxValue;
            maximumRequiredProductCount = 0;
            foreach (CustomerProjectOfferDefinition offer in project.Offers)
            {
                int requiredCount = 0;
                foreach (CustomerProjectLineDefinition line in offer.Lines)
                {
                    if (line.ProductType == productType)
                    {
                        requiredCount = line.RequiredCount;
                        break;
                    }
                }

                minimumRequiredProductCount = Math.Min(
                    minimumRequiredProductCount,
                    requiredCount);
                maximumRequiredProductCount = Math.Max(
                    maximumRequiredProductCount,
                    requiredCount);
            }

            if (minimumRequiredProductCount == int.MaxValue)
                throw new InvalidOperationException(
                    $"Project {project.ProjectType} has no procurement offers.");
        }

        private static ProcurementPurchaseState MapPurchaseState(
            ProcurementPurchaseAvailability availability) =>
            availability switch
            {
                ProcurementPurchaseAvailability.Available =>
                    ProcurementPurchaseState.Available,
                ProcurementPurchaseAvailability.InsufficientStorage =>
                    ProcurementPurchaseState.InsufficientStorage,
                ProcurementPurchaseAvailability.InsufficientMoney =>
                    ProcurementPurchaseState.InsufficientMoney,
                ProcurementPurchaseAvailability.DemandWouldBecomeInsolvent =>
                    ProcurementPurchaseState.PlanWouldBecomeUnfulfillable,
                _ => throw new ArgumentOutOfRangeException(nameof(availability), availability, null)
            };

        private static void ValidateTerminal(GameEntity player, GameEntity terminal)
        {
            if (!terminal.isProcurementTerminal || !terminal.hasEntityId ||
                !terminal.hasStoreEntityId || !terminal.hasStorageZoneEntityId ||
                !terminal.hasSelectedProductType ||
                terminal.EntityId != player.ProcurementTerminalEntityId)
            {
                throw new InvalidOperationException(
                    $"Player procurement modal references invalid terminal " +
                    $"{player.ProcurementTerminalEntityId}.");
            }
        }

        private static void ValidateStoreAndStorage(
            GameEntity terminal,
            GameEntity store,
            GameEntity storageZone)
        {
            if (!store.isStore || !store.hasEntityId || !store.hasMoney ||
                !store.hasProcurementTerminalEntityId ||
                !store.hasStorageZoneEntityId || store.Money < 0 ||
                store.ProcurementTerminalEntityId != terminal.EntityId ||
                store.StorageZoneEntityId != storageZone.EntityId ||
                terminal.StoreEntityId != store.EntityId)
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminal.EntityId} references an invalid store.");
            }
            if (!storageZone.isStorageZone || !storageZone.hasEntityId ||
                !storageZone.hasSlots || !storageZone.hasOccupiedStorageSlotCount ||
                storageZone.OccupiedStorageSlotCount < 0 ||
                storageZone.OccupiedStorageSlotCount > storageZone.Slots.Length ||
                terminal.StorageZoneEntityId != storageZone.EntityId)
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminal.EntityId} references an invalid " +
                    "storage zone.");
            }
        }

        private GameEntity[] GetConfirmedOrderLines(
            GameEntity visit,
            GameEntity store,
            GameEntity storageZone)
        {
            ValidateOrder(visit, store, storageZone);
            var indexedOrderLines =
                _gameContext.GetEntitiesWithOrderEntityId(visit.EntityId);
            foreach (GameEntity line in indexedOrderLines)
            {
                if (!line.hasLineIndex)
                {
                    throw new InvalidOperationException(
                        $"Order {visit.EntityId} contains a line without an index.");
                }
            }

            GameEntity[] orderLines = indexedOrderLines
                .OrderBy(line => line.LineIndex)
                .ToArray();
            ValidateOrderLines(visit, storageZone, orderLines);
            return orderLines;
        }

        private static void ValidateOrder(
            GameEntity visit,
            GameEntity store,
            GameEntity storageZone)
        {
            if (visit == null || !visit.isCustomerVisit || !visit.isOrder ||
                !visit.hasEntityId || !visit.hasCustomerProjectType ||
                !visit.hasStorageZoneEntityId ||
                !visit.isCustomerVisitLoading ||
                visit.StorageZoneEntityId != storageZone.EntityId)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} cannot present procurement without an active " +
                    "customer order.");
            }
        }

        private static void ValidateOrderLines(
            GameEntity visit,
            GameEntity storageZone,
            GameEntity[] lines)
        {
            if (lines.Length == 0 ||
                lines.Length > CustomerProjectConfig.MaxLinesPerOffer)
            {
                throw new InvalidOperationException(
                    $"Order {visit.EntityId} must expose between one and " +
                    $"{CustomerProjectConfig.MaxLinesPerOffer} product lines, found " +
                    $"{lines.Length}.");
            }

            for (int index = 0; index < lines.Length; index++)
            {
                GameEntity line = lines[index];
                if (!line.isOrderLine || line.isDestructed ||
                    !line.hasEntityId || !line.hasOrderEntityId ||
                    !line.hasStorageZoneEntityId || !line.hasLineIndex ||
                    !line.hasProductType || !line.hasRequiredProductCount ||
                    !line.hasAvailableProductCount || !line.hasLoadedProductCount ||
                    line.OrderEntityId != visit.EntityId ||
                    line.StorageZoneEntityId != storageZone.EntityId ||
                    line.LineIndex != index || line.RequiredProductCount <= 0 ||
                    line.AvailableProductCount < 0 || line.LoadedProductCount < 0 ||
                    line.LoadedProductCount > line.RequiredProductCount)
                {
                    throw new InvalidOperationException(
                        $"Order {visit.EntityId} has an invalid line at position {index}.");
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (lines[previous].ProductType == line.ProductType)
                    {
                        throw new InvalidOperationException(
                            $"Order {visit.EntityId} contains duplicate product type " +
                            $"{line.ProductType}.");
                    }
                }
            }
        }
    }
}
