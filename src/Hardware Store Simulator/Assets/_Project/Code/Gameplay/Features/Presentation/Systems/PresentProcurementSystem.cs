using System;
using System.Linq;
using Entitas;
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
        private readonly IHudService _hud;
        private readonly IGroup<GameEntity> _players;

        public PresentProcurementSystem(GameContext gameContext,
            IStaticDataService staticData, IHudService hud)
        {
            _gameContext = gameContext;
            _staticData = staticData;
            _hud = hud;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.ModalOpen,
                GameMatcher.ProcurementTerminalEntityId));
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

            ProductTypeId[] productTypes = _staticData.ProductTypes.ToArray();
            if (productTypes.Length != ProductCardCount)
            {
                throw new InvalidOperationException(
                    $"The prototype procurement catalog requires exactly {ProductCardCount} " +
                    $"product types, found {productTypes.Length}.");
            }

            int freeStorageSlotCount =
                storageZone.Slots.Length - storageZone.OccupiedStorageSlotCount;
            var products = new ProcurementProductSnapshot[productTypes.Length];
            for (int index = 0; index < productTypes.Length; index++)
            {
                products[index] = CreateProductSnapshot(
                    index,
                    productTypes[index],
                    terminal,
                    store,
                    freeStorageSlotCount,
                    orderLines);
            }

            _hud.PresentProcurement(new ProcurementSnapshot(
                visit.CustomerProjectType,
                store.Money,
                freeStorageSlotCount,
                products));
        }

        private ProcurementProductSnapshot CreateProductSnapshot(
            int index,
            ProductTypeId productType,
            GameEntity terminal,
            GameEntity store,
            int freeStorageSlotCount,
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

            int availableProductCount = orderLine?.AvailableProductCount ?? 0;
            int remainingRequiredProductCount = orderLine == null
                ? 0
                : orderLine.RequiredProductCount - orderLine.LoadedProductCount;
            int deficitProductCount = Math.Max(
                0,
                remainingRequiredProductCount - availableProductCount);
            DeliveryConfig delivery = _staticData.GetDelivery(productType);
            int moneyAfterPurchase = checked(store.Money - delivery.TotalCost);

            ProcurementPurchaseState purchaseState;
            if (orderLine == null)
            {
                purchaseState = ProcurementPurchaseState.NotRequired;
            }
            else if (deficitProductCount == 0)
            {
                purchaseState = ProcurementPurchaseState.StockSufficient;
            }
            else if (freeStorageSlotCount < delivery.ProductCount)
            {
                purchaseState = ProcurementPurchaseState.InsufficientStorage;
            }
            else if (moneyAfterPurchase < 0)
            {
                purchaseState = ProcurementPurchaseState.InsufficientMoney;
            }
            else
            {
                purchaseState = ProcurementPurchaseState.Available;
            }

            return new ProcurementProductSnapshot(
                index,
                productType,
                delivery.ProductCount,
                delivery.TotalCost,
                moneyAfterPurchase,
                availableProductCount,
                remainingRequiredProductCount,
                deficitProductCount,
                purchaseState,
                terminal.SelectedProductType == productType);
        }

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

        private static void ValidateOrder(
            GameEntity visit,
            GameEntity store,
            GameEntity storageZone)
        {
            if (visit == null || !visit.isCustomerVisit || !visit.isOrder ||
                !visit.hasEntityId || !visit.hasCustomerProjectType ||
                !visit.hasStorageZoneEntityId ||
                (!visit.isCustomerVisitWaiting && !visit.isCustomerVisitLoading) ||
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
