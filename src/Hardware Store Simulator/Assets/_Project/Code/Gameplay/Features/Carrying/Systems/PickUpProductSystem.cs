using System;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Carrying.Systems
{
    public sealed class PickUpProductSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public PickUpProductSystem(GameContext gameContext, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _requests = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.InteractionRequest,
                GameMatcher.SourceEntityId,
                GameMatcher.TargetEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity request in _requests)
            {
                GameEntity product = _gameContext.GetEntityWithEntityId(request.TargetEntityId);
                if (!product.isProduct)
                    continue;
                if (!product.isInteractable)
                    continue;

                GameEntity player = _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                if (player.isHandsOccupied)
                    continue;
                if (product.hasCarrierEntityId || product.isLoaded)
                    continue;

                GameEntity reservedOrderLine = null;
                bool canPickUp = product.isInboundProduct
                    ? CanPickUpInbound(product, player)
                    : CanPickUpStock(product, player, out reservedOrderLine);
                if (!canPickUp)
                    continue;

                if (product.isInStock)
                    ReserveStoragePlacement(product, reservedOrderLine);

                ReserveDeliverySlot(product);
                ReleaseTrolleySlot(product);
                ReleaseLoosePose(product);
                if (product.hasDeliverySlotIndex || product.hasStorageSlotIndex ||
                    product.hasOrderLineEntityId || product.hasLoadingSlotIndex ||
                    product.hasTrolleyEntityId || product.hasTrolleySlotIndex)
                {
                    throw new InvalidOperationException(
                        $"Product {product.EntityId} contains stale slot placement state.");
                }

                product.AddCarrierEntityId(player.EntityId);
                player.isHandsOccupied = true;
                player.isCarryingProduct = true;
                product.isInteractable = false;
                product.isProductPlacementDirty = true;
                _events.EmitAudio(AudioCueId.PickUp);
            }
        }

        private bool CanPickUpInbound(GameEntity product, GameEntity player)
        {
            if (!product.hasDeliveryEntityId)
                throw new InvalidOperationException(
                    $"Inbound product {product.EntityId} has no delivery relation.");

            GameEntity delivery = _gameContext.GetEntityWithEntityId(product.DeliveryEntityId);
            if (delivery == null || !delivery.isDelivery || !delivery.isDeliveryActive ||
                !delivery.hasEntityId || !delivery.hasSlots)
                throw new InvalidOperationException(
                    $"Inbound product {product.EntityId} is linked to an inactive delivery.");
            if (product.hasDeliverySlotIndex == product.hasReservedDeliverySlotIndex)
                throw new InvalidOperationException(
                    $"Inbound product {product.EntityId} must own exactly one delivery slot state.");

            int slotIndex = product.hasDeliverySlotIndex
                ? product.DeliverySlotIndex
                : product.ReservedDeliverySlotIndex;
            if (slotIndex < 0 || slotIndex >= delivery.Slots.Length)
                throw new InvalidOperationException(
                    $"Inbound product {product.EntityId} references invalid delivery slot " +
                    $"{slotIndex}.");

            return delivery.StoreEntityId == player.StoreEntityId;
        }

        private bool CanPickUpStock(
            GameEntity product,
            GameEntity player,
            out GameEntity reservedOrderLine)
        {
            reservedOrderLine = null;
            if (!product.isInStock)
                return false;
            if (!product.hasStorageZoneEntityId)
                throw new InvalidOperationException(
                    $"In-stock product {product.EntityId} has no storage ownership relation.");

            GameEntity store = _gameContext.GetEntityWithEntityId(player.StoreEntityId);
            if (store == null || store.isDestructed || !store.isStore ||
                !store.hasEntityId || !store.hasStorageZoneEntityId)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} references an invalid store.");
            }
            if (product.StorageZoneEntityId != store.StorageZoneEntityId)
                return false;

            GameEntity loadingBay =
                _gameContext.GetEntityWithCustomerLoadingBayStoreEntityId(store.EntityId);
            if (loadingBay == null || loadingBay.isDestructed ||
                !loadingBay.isCustomerLoadingBay || !loadingBay.hasEntityId ||
                !loadingBay.hasCustomerLoadingBayStoreEntityId ||
                loadingBay.CustomerLoadingBayStoreEntityId != store.EntityId)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} has an invalid customer loading bay relation.");
            }

            GameEntity customerVisit =
                _gameContext.GetEntityWithReservedCustomerLoadingBayEntityId(
                    loadingBay.EntityId);
            if (customerVisit == null)
                return false;
            ValidateLoadingBayReservation(customerVisit, loadingBay, store.EntityId);
            if (!customerVisit.isCustomerVisitLoading)
                return false;
            if (!customerVisit.isLoadingZone || !customerVisit.isOrder ||
                !customerVisit.hasStorageZoneEntityId)
                throw new InvalidOperationException(
                    $"Loading customer visit {customerVisit.EntityId} has incomplete order " +
                    "state.");

            if (product.hasReservedOrderLineEntityId)
            {
                reservedOrderLine = _gameContext.GetEntityWithEntityId(
                    product.ReservedOrderLineEntityId);
                ValidateOrderLine(customerVisit, reservedOrderLine);
                if (reservedOrderLine.ProductType != product.ProductType)
                    throw new InvalidOperationException(
                        $"Product {product.EntityId} reserves order line " +
                        $"{reservedOrderLine.EntityId} for another product type.");
                int existingReservationCount = CountReservedProducts(reservedOrderLine);
                if (reservedOrderLine.LoadedProductCount >=
                    reservedOrderLine.RequiredProductCount ||
                    reservedOrderLine.LoadedProductCount + existingReservationCount >
                    reservedOrderLine.RequiredProductCount)
                {
                    throw new InvalidOperationException(
                        $"Order line {reservedOrderLine.EntityId} has invalid reserved quota.");
                }

                return true;
            }

            GameEntity matchingLine = null;
            foreach (GameEntity line in
                     _gameContext.GetEntitiesWithOrderEntityId(customerVisit.EntityId))
            {
                ValidateOrderLine(customerVisit, line);
                if (line.ProductType != product.ProductType)
                    continue;
                if (matchingLine != null)
                    throw new InvalidOperationException(
                        $"Customer visit {customerVisit.EntityId} has duplicate order lines " +
                        $"for {product.ProductType}.");

                matchingLine = line;
            }

            if (matchingLine == null)
                return false;

            int reservedProductCount = CountReservedProducts(matchingLine);
            if (matchingLine.LoadedProductCount + reservedProductCount >=
                matchingLine.RequiredProductCount)
                return false;

            reservedOrderLine = matchingLine;
            return true;
        }

        private static void ValidateLoadingBayReservation(
            GameEntity visit,
            GameEntity loadingBay,
            int storeEntityId)
        {
            if (visit.isDestructed || !visit.isCustomerVisit ||
                !visit.isCustomerVehicle || !visit.hasEntityId ||
                !visit.hasCustomerVisitStoreEntityId ||
                visit.CustomerVisitStoreEntityId != storeEntityId ||
                !visit.hasReservedCustomerLoadingBayEntityId ||
                visit.ReservedCustomerLoadingBayEntityId != loadingBay.EntityId)
            {
                throw new InvalidOperationException(
                    $"Customer loading bay {loadingBay.EntityId} has an invalid reservation.");
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
                (visit.isCustomerVisitDeparting ? 1 : 0);
            if (lifecycleCount != 1)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} must have exactly one lifecycle marker.");
            }
        }

        private int CountReservedProducts(GameEntity orderLine)
        {
            int count = 0;
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithReservedOrderLineEntityId(orderLine.EntityId))
            {
                if (!product.isProduct || product.isDestructed || !product.isInStock ||
                    !product.hasEntityId || !product.hasProductType ||
                    !product.hasStorageZoneEntityId ||
                    !product.hasReservedStorageSlotIndex ||
                    product.ReservedOrderLineEntityId != orderLine.EntityId ||
                    product.ProductType != orderLine.ProductType ||
                    product.StorageZoneEntityId != orderLine.StorageZoneEntityId)
                {
                    throw new InvalidOperationException(
                        $"Order line {orderLine.EntityId} has an invalid product reservation.");
                }

                count++;
            }

            return count;
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

        private static void ReserveDeliverySlot(GameEntity product)
        {
            if (product.hasDeliverySlotIndex)
            {
                if (product.hasReservedDeliverySlotIndex)
                    throw new InvalidOperationException(
                        $"Inbound product {product.EntityId} already reserves a delivery slot.");

                int slotIndex = product.DeliverySlotIndex;
                product.RemoveDeliverySlotIndex();
                product.AddReservedDeliverySlotIndex(slotIndex);
            }
        }

        private static void ReserveStoragePlacement(
            GameEntity product,
            GameEntity orderLine)
        {
            if (!product.hasStorageZoneEntityId)
                throw new InvalidOperationException(
                    $"In-stock product {product.EntityId} has no storage ownership relation.");

            if (product.hasStorageSlotIndex)
            {
                if (product.hasReservedStorageSlotIndex)
                    throw new InvalidOperationException(
                        $"Stock product {product.EntityId} already reserves a storage slot.");

                int slotIndex = product.StorageSlotIndex;
                product.RemoveStorageSlotIndex();
                product.AddReservedStorageSlotIndex(slotIndex);
            }
            else if (!product.hasReservedStorageSlotIndex)
            {
                throw new InvalidOperationException(
                    $"Stock product {product.EntityId} has no storage slot reservation.");
            }

            if (product.hasReservedOrderLineEntityId)
            {
                if (product.ReservedOrderLineEntityId != orderLine.EntityId)
                    throw new InvalidOperationException(
                        $"Stock product {product.EntityId} reserves another order line.");
            }
            else
            {
                product.AddReservedOrderLineEntityId(orderLine.EntityId);
            }
        }

        private static void ReleaseLoosePose(GameEntity product)
        {
            if (!product.isLooseProduct)
            {
                if (product.hasWorldPosition || product.hasWorldRotation)
                    throw new InvalidOperationException(
                        $"Non-loose product {product.EntityId} contains a loose world pose.");

                return;
            }

            if (!product.hasWorldPosition || !product.hasWorldRotation)
                throw new InvalidOperationException(
                    $"Loose product {product.EntityId} has an incomplete world pose.");

            product.isLooseProduct = false;
            product.RemoveWorldPosition();
            product.RemoveWorldRotation();
        }

        private static void ReleaseTrolleySlot(GameEntity product)
        {
            if (product.hasTrolleyEntityId != product.hasTrolleySlotIndex)
                throw new InvalidOperationException(
                    $"Product {product.EntityId} has incomplete trolley placement state.");
            if (!product.hasTrolleyEntityId)
                return;

            product.RemoveTrolleyEntityId();
            product.RemoveTrolleySlotIndex();
        }
    }
}
