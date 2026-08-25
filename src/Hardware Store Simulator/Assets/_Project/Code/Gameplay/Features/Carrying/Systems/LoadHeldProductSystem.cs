using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common;

namespace HardwareStore.Gameplay.Features.Carrying.Systems
{
    public sealed class LoadHeldProductSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _requests;
        private readonly Dictionary<int, int> _occupiedLoadingSlots = new();

        public LoadHeldProductSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _requests = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.InteractionRequest,
                GameMatcher.SourceEntityId,
                GameMatcher.TargetEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity request in _requests)
            {
                GameEntity visit = _gameContext.GetEntityWithEntityId(request.TargetEntityId);
                if (visit == null)
                    throw new InvalidOperationException(
                        $"Product loading targets missing entity {request.TargetEntityId}.");
                if (!visit.isLoadingZone || !visit.isCustomerVisitLoading)
                    continue;

                GameEntity player = _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                if (player == null || player.isDestructed || !player.isPlayer ||
                    !player.hasEntityId || !player.hasStoreEntityId)
                {
                    throw new InvalidOperationException(
                        $"Interaction source cannot load customer visit {visit.EntityId}.");
                }
                ValidateLoadingVisit(visit, player.StoreEntityId);
                if (player.StoreEntityId != visit.CustomerVisitStoreEntityId ||
                    !player.isHandsOccupied || !player.isCarryingProduct)
                    continue;

                GameEntity product = _gameContext.GetEntityWithCarrierEntityId(player.EntityId);
                if (product.isInboundProduct)
                {
                    InboundProductManifestValidator.Validate(_gameContext, product);
                    continue;
                }
                if (!product.isInStock || product.isLoaded)
                    continue;
                ValidateHeldProduct(visit, product);
                GameEntity orderLine = _gameContext.GetEntityWithEntityId(
                    product.ReservedOrderLineEntityId);
                ValidateOrderLine(visit, orderLine);
                if (orderLine.EntityId != product.ReservedOrderLineEntityId ||
                    orderLine.ProductType != product.ProductType)
                {
                    throw new InvalidOperationException(
                        $"Held product {product.EntityId} does not satisfy its reserved order " +
                        $"line {product.ReservedOrderLineEntityId}.");
                }

                int linkedProductCount = CountLinkedProducts(orderLine);
                int reservedProductCount = CountReservedProducts(orderLine);
                if (linkedProductCount < orderLine.LoadedProductCount ||
                    linkedProductCount + reservedProductCount >
                    orderLine.RequiredProductCount)
                {
                    throw new InvalidOperationException(
                        $"Order line {orderLine.EntityId} has invalid linked/reserved quota.");
                }
                if (linkedProductCount >= orderLine.RequiredProductCount)
                    throw new InvalidOperationException(
                        $"Reserved order line {orderLine.EntityId} is already fully loaded.");
                int loadingSlotIndex = FindFreeLoadingSlot(visit);
                if (loadingSlotIndex < 0)
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has insufficient loading slots.");

                product.RemoveCarrierEntityId();
                player.isHandsOccupied = false;
                player.isCarryingProduct = false;
                product.isInStock = false;
                product.isLoaded = true;
                product.isInteractable = false;
                product.RemoveStorageZoneEntityId();
                product.RemoveReservedStorageSlotIndex();
                product.RemoveReservedOrderLineEntityId();
                product.AddOrderLineEntityId(orderLine.EntityId);
                product.AddLoadingSlotIndex(loadingSlotIndex);
                product.isProductLoaded = true;
                product.isProductPlacementDirty = true;
            }
        }

        private int FindFreeLoadingSlot(GameEntity visit)
        {
            _occupiedLoadingSlots.Clear();
            foreach (GameEntity line in
                     _gameContext.GetEntitiesWithOrderEntityId(visit.EntityId))
            {
                ValidateOrderLine(visit, line);
                foreach (GameEntity product in
                         _gameContext.GetEntitiesWithOrderLineEntityId(line.EntityId))
                {
                    ValidateLinkedProduct(line, product);
                    ReserveLoadingSlot(visit, product.LoadingSlotIndex,
                        product.EntityId);
                }
                foreach (GameEntity product in
                         _gameContext.GetEntitiesWithReservedOrderLineEntityId(
                             line.EntityId))
                {
                    if (!product.hasReservedCustomerLoadingSlotIndex)
                        continue;
                    ValidateBatchReservation(visit, line, product);
                    ReserveLoadingSlot(visit,
                        product.ReservedCustomerLoadingSlotIndex,
                        product.EntityId);
                }
            }
            foreach (GameEntity task in
                     _gameContext.GetEntitiesWithWarehouseTaskCustomerVisitEntityId(
                         visit.EntityId))
            {
                if (task.isDestructed ||
                    !task.hasWarehouseTaskReservedLoadingSlotIndex)
                {
                    continue;
                }
                if (!task.isWarehouseTask ||
                    !task.isStockToCustomerLoadingTask ||
                    task.isInboundToStorageTask || !task.hasEntityId ||
                    !task.hasWarehouseTaskStep)
                {
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has invalid worker loading " +
                        "reservation.");
                }
                ReserveLoadingSlot(visit,
                    task.WarehouseTaskReservedLoadingSlotIndex,
                    task.EntityId);
            }

            for (int slotIndex = 0; slotIndex < visit.Slots.Length; slotIndex++)
            {
                if (!_occupiedLoadingSlots.ContainsKey(slotIndex))
                    return slotIndex;
            }
            return -1;
        }

        private void ReserveLoadingSlot(GameEntity visit, int slotIndex,
            int ownerEntityId)
        {
            if (slotIndex < 0 || slotIndex >= visit.Slots.Length)
            {
                throw new InvalidOperationException(
                    $"Entity {ownerEntityId} reserves invalid loading slot " +
                    $"{slotIndex} for visit {visit.EntityId}.");
            }
            if (!_occupiedLoadingSlots.TryAdd(slotIndex, ownerEntityId))
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} loading slot {slotIndex} is " +
                    "occupied or reserved twice.");
            }
        }

        private int CountLinkedProducts(GameEntity line)
        {
            int count = 0;
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithOrderLineEntityId(line.EntityId))
            {
                ValidateLinkedProduct(line, product);
                count++;
            }
            return count;
        }

        private int CountReservedProducts(GameEntity line)
        {
            int count = 0;
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithReservedOrderLineEntityId(
                         line.EntityId))
            {
                if (product.isDestructed || !product.isProduct ||
                    !product.isInStock || !product.hasEntityId ||
                    !product.hasProductType ||
                    product.ProductType != line.ProductType ||
                    !product.hasStorageZoneEntityId ||
                    product.StorageZoneEntityId != line.StorageZoneEntityId ||
                    !product.hasReservedStorageSlotIndex)
                {
                    throw new InvalidOperationException(
                        $"Order line {line.EntityId} has an invalid product reservation.");
                }
                count++;
            }
            return count;
        }

        private static void ValidateLinkedProduct(GameEntity line,
            GameEntity product)
        {
            if (product.isDestructed || !product.isProduct ||
                !product.isLoaded || !product.hasEntityId ||
                !product.hasProductType ||
                product.ProductType != line.ProductType ||
                !product.hasOrderLineEntityId ||
                product.OrderLineEntityId != line.EntityId ||
                !product.hasLoadingSlotIndex)
            {
                throw new InvalidOperationException(
                    $"Order line {line.EntityId} has an invalid linked product.");
            }
        }

        private void ValidateBatchReservation(GameEntity visit,
            GameEntity line, GameEntity product)
        {
            if (product.isDestructed || !product.isProduct ||
                !product.isInStock || product.isInboundProduct ||
                product.isLoaded || product.isInteractable ||
                !product.hasEntityId || !product.hasProductType ||
                product.ProductType != line.ProductType ||
                !product.hasStorageZoneEntityId ||
                product.StorageZoneEntityId != line.StorageZoneEntityId ||
                !product.hasReservedStorageSlotIndex ||
                !product.hasReservedOrderLineEntityId ||
                product.ReservedOrderLineEntityId != line.EntityId ||
                !product.hasWarehouseRunEntityId)
                throw new InvalidOperationException(
                    $"Order line {line.EntityId} has an invalid batch reservation.");
            GameEntity run = _gameContext.GetEntityWithEntityId(
                product.WarehouseRunEntityId);
            if (run == null || run.isDestructed || !run.isWarehouseTask ||
                !run.isWorkerTrolleyCustomerLoadingRun ||
                !run.hasWarehouseTaskCustomerVisitEntityId ||
                run.WarehouseTaskCustomerVisitEntityId != visit.EntityId)
                throw new InvalidOperationException(
                    $"Batch product {product.EntityId} has an invalid trolley run.");
        }

        private void ValidateLoadingVisit(GameEntity visit, int storeEntityId)
        {
            if (visit.isDestructed || !visit.isCustomerVisit ||
                !visit.isCustomerVehicle || !visit.isCustomerVisitLoading ||
                !visit.hasEntityId || !visit.hasCustomerVisitStoreEntityId ||
                visit.CustomerVisitStoreEntityId != storeEntityId ||
                !visit.hasReservedCustomerLoadingBayEntityId ||
                !visit.isOrder || !visit.hasStorageZoneEntityId || !visit.hasSlots)
            {
                throw new InvalidOperationException(
                    $"Loading customer visit {visit.EntityId} has incomplete runtime state.");
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

            GameEntity loadingBay =
                _gameContext.GetEntityWithCustomerLoadingBayStoreEntityId(storeEntityId);
            if (loadingBay == null || loadingBay.isDestructed ||
                !loadingBay.isCustomerLoadingBay || !loadingBay.hasEntityId ||
                !loadingBay.hasCustomerLoadingBayStoreEntityId ||
                loadingBay.CustomerLoadingBayStoreEntityId != storeEntityId ||
                visit.ReservedCustomerLoadingBayEntityId != loadingBay.EntityId ||
                _gameContext.GetEntityWithReservedCustomerLoadingBayEntityId(
                    loadingBay.EntityId) != visit)
            {
                throw new InvalidOperationException(
                    $"Loading customer visit {visit.EntityId} does not reserve store " +
                    $"{storeEntityId} customer loading bay.");
            }
        }

        private static void ValidateHeldProduct(GameEntity visit, GameEntity product)
        {
            if (!product.hasStorageZoneEntityId)
                throw new InvalidOperationException(
                    $"In-stock product {product.EntityId} has no storage ownership relation.");
            if (product.StorageZoneEntityId != visit.StorageZoneEntityId)
                throw new InvalidOperationException(
                    $"Held product {product.EntityId} belongs to storage " +
                    $"{product.StorageZoneEntityId}, not customer visit storage " +
                    $"{visit.StorageZoneEntityId}.");
            if (product.hasStorageSlotIndex)
                throw new InvalidOperationException(
                    $"Carried product {product.EntityId} still occupies storage slot " +
                    $"{product.StorageSlotIndex}.");
            if (!product.hasReservedStorageSlotIndex)
                throw new InvalidOperationException(
                    $"Carried product {product.EntityId} has no reserved storage slot.");
            if (!product.hasReservedOrderLineEntityId)
                throw new InvalidOperationException(
                    $"Carried product {product.EntityId} has no reserved order line.");
            if (product.isLooseProduct || product.hasWorldPosition || product.hasWorldRotation)
                throw new InvalidOperationException(
                    $"Carried product {product.EntityId} still contains loose placement state.");
            if (product.hasOrderLineEntityId || product.hasLoadingSlotIndex ||
                product.hasDeliverySlotIndex || product.hasReservedDeliverySlotIndex ||
                product.hasTrolleyEntityId || product.hasTrolleySlotIndex)
            {
                throw new InvalidOperationException(
                    $"Carried product {product.EntityId} already contains customer loading state.");
            }
        }

        private static void ValidateOrderLine(GameEntity visit, GameEntity line)
        {
            if (!line.isOrderLine || line.isDestructed || !line.hasEntityId ||
                !line.hasOrderEntityId || !line.hasStorageZoneEntityId ||
                !line.hasProductType || !line.hasRequiredProductCount ||
                !line.hasLoadedProductCount)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} has an invalid order line.");
            }
            if (line.OrderEntityId != visit.EntityId ||
                line.StorageZoneEntityId != visit.StorageZoneEntityId ||
                line.LoadedProductCount < 0 ||
                line.LoadedProductCount > line.RequiredProductCount)
            {
                throw new InvalidOperationException(
                    $"Order line {line.EntityId} has invalid runtime state.");
            }
        }
    }
}
