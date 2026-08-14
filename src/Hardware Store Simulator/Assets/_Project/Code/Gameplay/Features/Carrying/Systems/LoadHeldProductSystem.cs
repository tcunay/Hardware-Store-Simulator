using System;
using Entitas;

namespace HardwareStore.Gameplay.Features.Carrying.Systems
{
    public sealed class LoadHeldProductSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _requests;

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

                int totalLoadedProductCount = 0;
                foreach (GameEntity line in
                         _gameContext.GetEntitiesWithOrderEntityId(visit.EntityId))
                {
                    ValidateOrderLine(visit, line);
                    totalLoadedProductCount = checked(
                        totalLoadedProductCount + line.LoadedProductCount);
                }

                if (orderLine.LoadedProductCount >= orderLine.RequiredProductCount)
                    throw new InvalidOperationException(
                        $"Reserved order line {orderLine.EntityId} is already fully loaded.");
                if (visit.Slots.Length <= totalLoadedProductCount)
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
                product.AddLoadingSlotIndex(totalLoadedProductCount);
                product.isProductLoaded = true;
                product.isProductPlacementDirty = true;
            }
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
