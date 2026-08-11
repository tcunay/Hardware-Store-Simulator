using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Features.Delivery.Systems
{
    public sealed class StoreInboundProductSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;
        private readonly IGroup<GameEntity> _stockedProducts;
        private readonly IGroup<GameEntity> _reservedStockProducts;

        public StoreInboundProductSystem(GameContext gameContext, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _requests = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.InteractionRequest,
                GameMatcher.SourceEntityId,
                GameMatcher.TargetEntityId));
            _stockedProducts = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Product,
                GameMatcher.EntityId,
                GameMatcher.InStock,
                GameMatcher.StorageZoneEntityId,
                GameMatcher.StorageSlotIndex));
            _reservedStockProducts = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Product,
                GameMatcher.EntityId,
                GameMatcher.InStock,
                GameMatcher.StorageZoneEntityId,
                GameMatcher.ReservedStorageSlotIndex));
        }

        public void Execute()
        {
            Dictionary<int, Dictionary<int, int>> occupiedSlotsByZone =
                CollectOccupiedSlots();

            foreach (GameEntity request in _requests)
            {
                GameEntity storageZone =
                    _gameContext.GetEntityWithEntityId(request.TargetEntityId);
                if (storageZone == null)
                    throw new InvalidOperationException(
                        $"Interaction target {request.TargetEntityId} does not exist.");
                if (!storageZone.isStorageZone)
                    continue;

                GameEntity player =
                    _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                if (player == null)
                    throw new InvalidOperationException(
                        $"Interaction source {request.SourceEntityId} does not exist.");

                GameEntity store =
                    _gameContext.GetEntityWithEntityId(player.StoreEntityId);
                if (store == null || !store.isStoreSceneBindingsValidated)
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} is linked to an unconfigured store.");
                if (store.StorageZoneEntityId != storageZone.EntityId)
                    continue;
                if (!player.isHandsOccupied || !player.isCarryingProduct)
                    continue;

                GameEntity product =
                    _gameContext.GetEntityWithCarrierEntityId(player.EntityId);
                if (product.isInStock)
                {
                    ReturnStockProduct(
                        player,
                        product,
                        storageZone,
                        occupiedSlotsByZone);
                    continue;
                }

                if (!product.isInboundProduct || product.isInStock ||
                    !product.hasDeliveryEntityId)
                    continue;
                if (product.hasDeliverySlotIndex ||
                    !product.hasReservedDeliverySlotIndex ||
                    product.hasTrolleyEntityId || product.hasTrolleySlotIndex)
                    throw new InvalidOperationException(
                        $"Carried inbound product {product.EntityId} has invalid delivery slot " +
                        "reservation state.");
                if (product.isLooseProduct || product.hasWorldPosition || product.hasWorldRotation)
                    throw new InvalidOperationException(
                        $"Carried inbound product {product.EntityId} still contains loose placement state.");

                GameEntity delivery =
                    _gameContext.GetEntityWithEntityId(product.DeliveryEntityId);
                if (delivery == null || !delivery.isDeliveryActive ||
                    delivery.StoreEntityId != store.EntityId)
                {
                    throw new InvalidOperationException(
                        $"Inbound product {product.EntityId} does not belong to store {store.EntityId}.");
                }

                Dictionary<int, int> occupiedSlots = GetOrCreateOccupiedSlots(
                    occupiedSlotsByZone,
                    storageZone.EntityId);
                int slotIndex = FindFreeSlot(storageZone.Slots.Length, occupiedSlots);
                if (slotIndex < 0)
                {
                    _events.EmitNotification(LocalizedTexts.Text(
                        LocalizationKey.NotificationStorageFull));
                    continue;
                }

                player.isHandsOccupied = false;
                player.isCarryingProduct = false;
                product.RemoveCarrierEntityId();
                product.isInboundProduct = false;
                product.isInStock = true;
                product.isInteractable = true;
                product.RemoveReservedDeliverySlotIndex();
                product.AddStorageZoneEntityId(storageZone.EntityId);
                product.AddStorageSlotIndex(slotIndex);
                product.isProductPlacementDirty = true;
                product.isProductStocked = true;
                occupiedSlots.Add(slotIndex, product.EntityId);
            }
        }

        private void ReturnStockProduct(
            GameEntity player,
            GameEntity product,
            GameEntity storageZone,
            Dictionary<int, Dictionary<int, int>> occupiedSlotsByZone)
        {
            if (!product.hasStorageZoneEntityId ||
                product.StorageZoneEntityId != storageZone.EntityId ||
                product.hasStorageSlotIndex || !product.hasReservedStorageSlotIndex ||
                !product.hasReservedOrderLineEntityId || product.isInboundProduct ||
                product.hasDeliveryEntityId || product.hasReservedDeliverySlotIndex ||
                product.isLooseProduct || product.hasWorldPosition ||
                product.hasWorldRotation || product.hasOrderLineEntityId ||
                product.hasLoadingSlotIndex || product.hasTrolleyEntityId ||
                product.hasTrolleySlotIndex)
            {
                throw new InvalidOperationException(
                    $"Held stock product {product.EntityId} has invalid storage return state.");
            }

            int slotIndex = product.ReservedStorageSlotIndex;
            if (slotIndex < 0 || slotIndex >= storageZone.Slots.Length)
                throw new InvalidOperationException(
                    $"Stock product {product.EntityId} reserves invalid storage slot " +
                    $"{slotIndex}.");

            Dictionary<int, int> occupiedSlots = GetOrCreateOccupiedSlots(
                occupiedSlotsByZone,
                storageZone.EntityId);
            if (!occupiedSlots.TryGetValue(slotIndex, out int reservationOwner) ||
                reservationOwner != product.EntityId)
            {
                throw new InvalidOperationException(
                    $"Stock product {product.EntityId} does not exclusively own reserved " +
                    $"storage slot {slotIndex}.");
            }

            GameEntity orderLine = _gameContext.GetEntityWithEntityId(
                product.ReservedOrderLineEntityId);
            if (orderLine == null || !orderLine.isOrderLine || orderLine.isDestructed ||
                !orderLine.hasEntityId || !orderLine.hasOrderEntityId ||
                !orderLine.hasStorageZoneEntityId || !orderLine.hasProductType ||
                orderLine.EntityId != product.ReservedOrderLineEntityId ||
                orderLine.ProductType != product.ProductType ||
                orderLine.StorageZoneEntityId != storageZone.EntityId)
            {
                throw new InvalidOperationException(
                    $"Stock product {product.EntityId} references invalid reserved order line " +
                    $"{product.ReservedOrderLineEntityId}.");
            }
            GameEntity visit = _gameContext.GetEntityWithEntityId(orderLine.OrderEntityId);
            if (visit == null || !visit.isCustomerVisitLoading || !visit.isOrder ||
                !visit.hasEntityId || visit.EntityId != orderLine.OrderEntityId)
            {
                throw new InvalidOperationException(
                    $"Reserved order line {orderLine.EntityId} belongs to an inactive order.");
            }

            player.isHandsOccupied = false;
            player.isCarryingProduct = false;
            product.RemoveCarrierEntityId();
            product.RemoveReservedStorageSlotIndex();
            product.RemoveReservedOrderLineEntityId();
            product.AddStorageSlotIndex(slotIndex);
            product.isInteractable = true;
            product.isProductPlacementDirty = true;
        }

        private Dictionary<int, Dictionary<int, int>> CollectOccupiedSlots()
        {
            var occupiedSlotsByZone = new Dictionary<int, Dictionary<int, int>>();
            foreach (GameEntity product in _stockedProducts)
            {
                if (product.hasReservedStorageSlotIndex)
                    throw new InvalidOperationException(
                        $"Stock product {product.EntityId} has active and reserved storage slots.");
                AddOccupiedSlot(
                    occupiedSlotsByZone,
                    product,
                    product.StorageSlotIndex);
            }
            foreach (GameEntity product in _reservedStockProducts)
            {
                if (product.hasStorageSlotIndex || !product.hasReservedOrderLineEntityId)
                    throw new InvalidOperationException(
                        $"Stock product {product.EntityId} has invalid storage reservation state.");
                AddOccupiedSlot(
                    occupiedSlotsByZone,
                    product,
                    product.ReservedStorageSlotIndex);
            }

            return occupiedSlotsByZone;
        }

        private static void AddOccupiedSlot(
            Dictionary<int, Dictionary<int, int>> occupiedSlotsByZone,
            GameEntity product,
            int slotIndex)
        {
            Dictionary<int, int> occupied = GetOrCreateOccupiedSlots(
                occupiedSlotsByZone,
                product.StorageZoneEntityId);
            if (!occupied.TryAdd(slotIndex, product.EntityId))
                throw new InvalidOperationException(
                    $"Storage slot {slotIndex} in zone " +
                    $"{product.StorageZoneEntityId} is occupied more than once.");
        }

        private static Dictionary<int, int> GetOrCreateOccupiedSlots(
            Dictionary<int, Dictionary<int, int>> occupiedSlotsByZone,
            int storageZoneEntityId)
        {
            if (!occupiedSlotsByZone.TryGetValue(
                    storageZoneEntityId,
                    out Dictionary<int, int> occupied))
            {
                occupied = new Dictionary<int, int>();
                occupiedSlotsByZone.Add(storageZoneEntityId, occupied);
            }

            return occupied;
        }

        private static int FindFreeSlot(
            int slotCount,
            IReadOnlyDictionary<int, int> occupiedSlots)
        {
            for (int index = 0; index < slotCount; index++)
            {
                if (!occupiedSlots.ContainsKey(index))
                    return index;
            }

            return -1;
        }
    }
}
