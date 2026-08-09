using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Delivery.Systems
{
    public sealed class StoreInboundProductSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;
        private readonly IGroup<GameEntity> _stockedProducts;

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
                GameMatcher.InStock,
                GameMatcher.StorageZoneEntityId,
                GameMatcher.StorageSlotIndex));
        }

        public void Execute()
        {
            Dictionary<int, HashSet<int>> occupiedSlotsByZone = CollectOccupiedSlots();

            foreach (GameEntity request in _requests)
            {
                GameEntity storageZone = _gameContext.GetRequiredEntity(
                    request.TargetEntityId,
                    "interaction target");
                if (!storageZone.isStorageZone)
                    continue;

                GameEntity player = _gameContext.GetRequiredEntity(
                    request.SourceEntityId,
                    "interaction source");
                if (!player.isPlayer)
                    throw new InvalidOperationException(
                        $"Interaction source {request.SourceEntityId} is not a player.");

                GameEntity store = GetPlayerStore(player);
                if (!store.hasStorageZoneEntityId)
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} has no storage relation.");
                if (store.StorageZoneEntityId != storageZone.EntityId)
                    continue;
                if (!player.hasHeldProductId)
                    continue;
                if (!storageZone.hasSlots)
                    throw new InvalidOperationException(
                        $"Storage zone {storageZone.EntityId} has no registered slots.");

                GameEntity product = _gameContext.GetRequiredEntity(
                    player.HeldProductId,
                    "player held product");
                if (!product.isProduct || !product.isCarried || !product.isInboundProduct ||
                    product.isInStock || !product.hasDeliveryEntityId)
                    continue;
                if (product.hasDeliverySlotIndex)
                    throw new InvalidOperationException(
                        $"Carried inbound product {product.EntityId} still occupies a delivery slot.");
                if (product.isLooseProduct || product.hasWorldPosition || product.hasWorldRotation)
                    throw new InvalidOperationException(
                        $"Carried inbound product {product.EntityId} still contains loose placement state.");

                GameEntity delivery = _gameContext.GetRequiredEntity(
                    product.DeliveryEntityId,
                    "inbound product delivery");
                if (!delivery.isDelivery || !delivery.isDeliveryActive ||
                    !delivery.hasStoreEntityId || delivery.StoreEntityId != store.EntityId)
                {
                    throw new InvalidOperationException(
                        $"Inbound product {product.EntityId} does not belong to store {store.EntityId}.");
                }

                HashSet<int> occupiedSlots = GetOrCreateOccupiedSlots(
                    occupiedSlotsByZone,
                    storageZone.EntityId);
                int slotIndex = FindFreeSlot(storageZone.Slots.Length, occupiedSlots);
                if (slotIndex < 0)
                {
                    _events.EmitNotification("На складе нет свободного места");
                    continue;
                }

                int productEntityId = product.EntityId;
                int deliveryEntityId = delivery.EntityId;

                player.RemoveHeldProductId();
                product.isCarried = false;
                product.isInboundProduct = false;
                product.isInStock = true;
                product.isInteractable = true;
                product.AddStorageZoneEntityId(storageZone.EntityId);
                product.AddStorageSlotIndex(slotIndex);
                product.isProductPlacementDirty = true;
                _events.EmitProductStocked(productEntityId, deliveryEntityId);
                product.RemoveDeliveryEntityId();
                occupiedSlots.Add(slotIndex);
            }
        }

        private GameEntity GetPlayerStore(GameEntity player)
        {
            if (!player.hasStoreEntityId)
                throw new InvalidOperationException(
                    $"Player {player.EntityId} has no store relation.");

            GameEntity store = _gameContext.GetRequiredEntity(player.StoreEntityId, "player store");
            if (!store.isStore)
                throw new InvalidOperationException($"Entity {player.StoreEntityId} is not a store.");

            return store;
        }

        private Dictionary<int, HashSet<int>> CollectOccupiedSlots()
        {
            var occupiedSlotsByZone = new Dictionary<int, HashSet<int>>();
            foreach (GameEntity product in _stockedProducts)
            {
                HashSet<int> occupied = GetOrCreateOccupiedSlots(
                    occupiedSlotsByZone,
                    product.StorageZoneEntityId);
                if (!occupied.Add(product.StorageSlotIndex))
                    throw new InvalidOperationException(
                        $"Storage slot {product.StorageSlotIndex} in zone " +
                        $"{product.StorageZoneEntityId} is occupied more than once.");
            }

            return occupiedSlotsByZone;
        }

        private static HashSet<int> GetOrCreateOccupiedSlots(
            Dictionary<int, HashSet<int>> occupiedSlotsByZone,
            int storageZoneEntityId)
        {
            if (!occupiedSlotsByZone.TryGetValue(storageZoneEntityId, out HashSet<int> occupied))
            {
                occupied = new HashSet<int>();
                occupiedSlotsByZone.Add(storageZoneEntityId, occupied);
            }

            return occupied;
        }

        private static int FindFreeSlot(int slotCount, HashSet<int> occupiedSlots)
        {
            for (int index = 0; index < slotCount; index++)
            {
                if (!occupiedSlots.Contains(index))
                    return index;
            }

            return -1;
        }
    }
}
