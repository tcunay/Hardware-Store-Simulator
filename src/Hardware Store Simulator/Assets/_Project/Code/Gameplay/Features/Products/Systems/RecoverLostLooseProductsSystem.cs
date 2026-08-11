using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Products.Systems
{
    public sealed class RecoverLostLooseProductsSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly float _minimumWorldY;
        private readonly IGroup<GameEntity> _looseInboundProducts;
        private readonly IGroup<GameEntity> _looseStockProducts;
        private readonly IGroup<GameEntity> _activeDeliverySlots;
        private readonly IGroup<GameEntity> _reservedDeliverySlots;
        private readonly IGroup<GameEntity> _activeStorageSlots;
        private readonly IGroup<GameEntity> _reservedStorageSlots;
        private readonly List<GameEntity> _inboundBuffer = new(8);
        private readonly List<GameEntity> _stockBuffer = new(8);

        public RecoverLostLooseProductsSystem(
            GameContext gameContext,
            IStaticDataService staticData,
            IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _minimumWorldY = staticData.ProductRecovery.MinimumWorldY;
            _looseInboundProducts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.EntityId,
                    GameMatcher.InboundProduct,
                    GameMatcher.DeliveryEntityId,
                    GameMatcher.ReservedDeliverySlotIndex,
                    GameMatcher.LooseProduct,
                    GameMatcher.WorldPosition,
                    GameMatcher.WorldRotation)
                .NoneOf(
                    GameMatcher.InStock,
                    GameMatcher.CarrierEntityId,
                    GameMatcher.Loaded,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.OrderLineEntityId,
                    GameMatcher.ReservedOrderLineEntityId,
                    GameMatcher.DeliverySlotIndex,
                    GameMatcher.StorageSlotIndex,
                    GameMatcher.ReservedStorageSlotIndex,
                    GameMatcher.LoadingSlotIndex,
                    GameMatcher.Destructed));
            _looseStockProducts = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.EntityId,
                    GameMatcher.InStock,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.ReservedStorageSlotIndex,
                    GameMatcher.ReservedOrderLineEntityId,
                    GameMatcher.LooseProduct,
                    GameMatcher.WorldPosition,
                    GameMatcher.WorldRotation)
                .NoneOf(
                    GameMatcher.InboundProduct,
                    GameMatcher.DeliveryEntityId,
                    GameMatcher.CarrierEntityId,
                    GameMatcher.Loaded,
                    GameMatcher.OrderLineEntityId,
                    GameMatcher.DeliverySlotIndex,
                    GameMatcher.ReservedDeliverySlotIndex,
                    GameMatcher.StorageSlotIndex,
                    GameMatcher.LoadingSlotIndex,
                    GameMatcher.Destructed));
            _activeDeliverySlots = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Product,
                GameMatcher.EntityId,
                GameMatcher.InboundProduct,
                GameMatcher.DeliveryEntityId,
                GameMatcher.DeliverySlotIndex));
            _reservedDeliverySlots = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Product,
                GameMatcher.EntityId,
                GameMatcher.InboundProduct,
                GameMatcher.DeliveryEntityId,
                GameMatcher.ReservedDeliverySlotIndex));
            _activeStorageSlots = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Product,
                GameMatcher.EntityId,
                GameMatcher.InStock,
                GameMatcher.StorageZoneEntityId,
                GameMatcher.StorageSlotIndex));
            _reservedStorageSlots = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Product,
                GameMatcher.EntityId,
                GameMatcher.InStock,
                GameMatcher.StorageZoneEntityId,
                GameMatcher.ReservedStorageSlotIndex));
        }

        public void Execute()
        {
            CollectLost(_looseInboundProducts, _inboundBuffer);
            CollectLost(_looseStockProducts, _stockBuffer);
            int recoveredCount = _inboundBuffer.Count + _stockBuffer.Count;
            if (recoveredCount == 0)
                return;

            Dictionary<(int OwnerEntityId, int SlotIndex), int> deliveryReservations =
                CollectDeliveryReservations();
            Dictionary<(int OwnerEntityId, int SlotIndex), int> storageReservations =
                CollectStorageReservations();

            foreach (GameEntity product in _inboundBuffer)
                RecoverInbound(product, deliveryReservations);
            foreach (GameEntity product in _stockBuffer)
                RecoverStock(product, storageReservations);

            _events.EmitNotification(LocalizedTexts.Text(
                LocalizationKey.NotificationProductsRecovered,
                recoveredCount));
        }

        private void CollectLost(
            IGroup<GameEntity> products,
            List<GameEntity> buffer)
        {
            buffer.Clear();
            foreach (GameEntity product in products)
            {
                if (product.WorldPosition.y < _minimumWorldY)
                    buffer.Add(product);
            }

            buffer.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        private Dictionary<(int OwnerEntityId, int SlotIndex), int>
            CollectDeliveryReservations()
        {
            var reservations = new Dictionary<(int, int), int>();
            foreach (GameEntity product in _activeDeliverySlots)
            {
                ValidateActiveDeliveryPlacement(product);
                AddReservation(
                    reservations,
                    product.DeliveryEntityId,
                    product.DeliverySlotIndex,
                    product.EntityId,
                    "Delivery");
            }
            foreach (GameEntity product in _reservedDeliverySlots)
            {
                ValidateReservedDeliveryPlacement(product);
                AddReservation(
                    reservations,
                    product.DeliveryEntityId,
                    product.ReservedDeliverySlotIndex,
                    product.EntityId,
                    "Delivery");
            }

            return reservations;
        }

        private Dictionary<(int OwnerEntityId, int SlotIndex), int>
            CollectStorageReservations()
        {
            var reservations = new Dictionary<(int, int), int>();
            foreach (GameEntity product in _activeStorageSlots)
            {
                ValidateActiveStoragePlacement(product);
                AddReservation(
                    reservations,
                    product.StorageZoneEntityId,
                    product.StorageSlotIndex,
                    product.EntityId,
                    "Storage");
            }
            foreach (GameEntity product in _reservedStorageSlots)
            {
                ValidateReservedStoragePlacement(product);
                AddReservation(
                    reservations,
                    product.StorageZoneEntityId,
                    product.ReservedStorageSlotIndex,
                    product.EntityId,
                    "Storage");
            }

            return reservations;
        }

        private void RecoverInbound(
            GameEntity product,
            IReadOnlyDictionary<(int OwnerEntityId, int SlotIndex), int> reservations)
        {
            GameEntity delivery =
                _gameContext.GetEntityWithEntityId(product.DeliveryEntityId);
            if (delivery == null || !delivery.isDelivery || !delivery.isDeliveryActive ||
                !delivery.hasEntityId || !delivery.hasSlots ||
                !delivery.hasProductType)
            {
                throw new InvalidOperationException(
                    $"Lost inbound product {product.EntityId} references invalid delivery " +
                    $"{product.DeliveryEntityId}.");
            }
            ValidateDeliveryRelations(delivery, product);

            int slotIndex = product.ReservedDeliverySlotIndex;
            ValidateReservedSlot(
                reservations,
                delivery.EntityId,
                delivery.Slots.Length,
                slotIndex,
                product.EntityId,
                "delivery");

            product.isLooseProduct = false;
            product.RemoveWorldPosition();
            product.RemoveWorldRotation();
            product.RemoveReservedDeliverySlotIndex();
            product.AddDeliverySlotIndex(slotIndex);
            product.isProductPlacementDirty = true;
        }

        private void ValidateDeliveryRelations(
            GameEntity delivery,
            GameEntity expectedProduct)
        {
            bool expectedProductFound = false;
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithDeliveryEntityId(delivery.EntityId))
            {
                if (!product.isProduct || product.isDestructed ||
                    !product.isInboundProduct || product.isInStock || product.isLoaded ||
                    !product.hasEntityId || !product.hasProductType ||
                    product.DeliveryEntityId != delivery.EntityId ||
                    product.ProductType != delivery.ProductType ||
                    product.hasDeliverySlotIndex == product.hasReservedDeliverySlotIndex)
                {
                    throw new InvalidOperationException(
                        $"Delivery {delivery.EntityId} has an invalid linked product.");
                }

                if (product == expectedProduct)
                    expectedProductFound = true;
            }

            if (!expectedProductFound)
                throw new InvalidOperationException(
                    $"Delivery {delivery.EntityId} does not index lost product " +
                    $"{expectedProduct.EntityId}.");
        }

        private void RecoverStock(
            GameEntity product,
            IReadOnlyDictionary<(int OwnerEntityId, int SlotIndex), int> reservations)
        {
            GameEntity storageZone =
                _gameContext.GetEntityWithEntityId(product.StorageZoneEntityId);
            if (storageZone == null || !storageZone.isStorageZone ||
                !storageZone.hasEntityId || !storageZone.hasSlots)
            {
                throw new InvalidOperationException(
                    $"Lost stock product {product.EntityId} references invalid storage zone " +
                    $"{product.StorageZoneEntityId}.");
            }

            ValidateReservedOrderLine(product);
            int slotIndex = product.ReservedStorageSlotIndex;
            ValidateReservedSlot(
                reservations,
                storageZone.EntityId,
                storageZone.Slots.Length,
                slotIndex,
                product.EntityId,
                "storage");

            product.isLooseProduct = false;
            product.RemoveWorldPosition();
            product.RemoveWorldRotation();
            product.RemoveReservedStorageSlotIndex();
            product.RemoveReservedOrderLineEntityId();
            product.AddStorageSlotIndex(slotIndex);
            product.isProductPlacementDirty = true;
        }

        private void ValidateReservedOrderLine(GameEntity product)
        {
            GameEntity orderLine = _gameContext.GetEntityWithEntityId(
                product.ReservedOrderLineEntityId);
            if (orderLine == null || !orderLine.isOrderLine || orderLine.isDestructed ||
                !orderLine.hasEntityId || !orderLine.hasOrderEntityId ||
                !orderLine.hasStorageZoneEntityId || !orderLine.hasProductType ||
                orderLine.EntityId != product.ReservedOrderLineEntityId ||
                orderLine.ProductType != product.ProductType ||
                orderLine.StorageZoneEntityId != product.StorageZoneEntityId)
            {
                throw new InvalidOperationException(
                    $"Lost stock product {product.EntityId} references invalid reserved " +
                    $"order line {product.ReservedOrderLineEntityId}.");
            }

            GameEntity visit = _gameContext.GetEntityWithEntityId(orderLine.OrderEntityId);
            if (visit == null || !visit.isCustomerVisitLoading || !visit.isOrder ||
                !visit.hasEntityId || visit.EntityId != orderLine.OrderEntityId)
            {
                throw new InvalidOperationException(
                    $"Reserved order line {orderLine.EntityId} belongs to an inactive order.");
            }
        }

        private static void AddReservation(
            Dictionary<(int OwnerEntityId, int SlotIndex), int> reservations,
            int ownerEntityId,
            int slotIndex,
            int productEntityId,
            string placementName)
        {
            if (!reservations.TryAdd((ownerEntityId, slotIndex), productEntityId))
            {
                throw new InvalidOperationException(
                    $"{placementName} slot {slotIndex} of entity {ownerEntityId} is " +
                    "reserved by more than one product.");
            }
        }

        private static void ValidateReservedSlot(
            IReadOnlyDictionary<(int OwnerEntityId, int SlotIndex), int> reservations,
            int ownerEntityId,
            int slotCount,
            int slotIndex,
            int productEntityId,
            string placementName)
        {
            if (slotIndex < 0 || slotIndex >= slotCount)
            {
                throw new InvalidOperationException(
                    $"Product {productEntityId} reserves invalid {placementName} slot " +
                    $"{slotIndex} of {slotCount}.");
            }
            if (!reservations.TryGetValue(
                    (ownerEntityId, slotIndex),
                    out int reservationOwner) || reservationOwner != productEntityId)
            {
                throw new InvalidOperationException(
                    $"Product {productEntityId} does not exclusively own reserved " +
                    $"{placementName} slot {slotIndex} of entity {ownerEntityId}.");
            }
        }

        private static void ValidateActiveDeliveryPlacement(GameEntity product)
        {
            if (product.hasReservedDeliverySlotIndex || product.hasCarrierEntityId ||
                product.isLooseProduct || product.isInStock || product.isLoaded ||
                product.hasStorageZoneEntityId || product.hasStorageSlotIndex ||
                product.hasReservedStorageSlotIndex || product.hasOrderLineEntityId ||
                product.hasReservedOrderLineEntityId || product.hasLoadingSlotIndex)
            {
                throw new InvalidOperationException(
                    $"Inbound product {product.EntityId} has conflicting delivery placement.");
            }
        }

        private static void ValidateReservedDeliveryPlacement(GameEntity product)
        {
            if (product.hasDeliverySlotIndex || product.isInStock || product.isLoaded ||
                product.hasStorageZoneEntityId || product.hasStorageSlotIndex ||
                product.hasReservedStorageSlotIndex || product.hasOrderLineEntityId ||
                product.hasReservedOrderLineEntityId || product.hasLoadingSlotIndex ||
                (product.hasCarrierEntityId && product.isLooseProduct))
            {
                throw new InvalidOperationException(
                    $"Inbound product {product.EntityId} has invalid delivery reservation state.");
            }
        }

        private static void ValidateActiveStoragePlacement(GameEntity product)
        {
            if (product.hasReservedStorageSlotIndex || product.hasCarrierEntityId ||
                product.isLooseProduct || product.isInboundProduct || product.isLoaded ||
                product.hasDeliveryEntityId || product.hasDeliverySlotIndex ||
                product.hasReservedDeliverySlotIndex || product.hasOrderLineEntityId ||
                product.hasReservedOrderLineEntityId || product.hasLoadingSlotIndex)
            {
                throw new InvalidOperationException(
                    $"Stock product {product.EntityId} has conflicting storage placement.");
            }
        }

        private static void ValidateReservedStoragePlacement(GameEntity product)
        {
            if (!product.hasReservedOrderLineEntityId || product.hasStorageSlotIndex ||
                product.isInboundProduct || product.isLoaded ||
                product.hasDeliveryEntityId || product.hasDeliverySlotIndex ||
                product.hasReservedDeliverySlotIndex || product.hasOrderLineEntityId ||
                product.hasLoadingSlotIndex ||
                (product.hasCarrierEntityId && product.isLooseProduct))
            {
                throw new InvalidOperationException(
                    $"Stock product {product.EntityId} has invalid storage reservation state.");
            }
        }
    }
}
