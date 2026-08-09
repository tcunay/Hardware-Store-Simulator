using System;
using System.Collections.Generic;
using Entitas;
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
                if (!player.isHandsOccupied)
                    continue;

                GameEntity product =
                    _gameContext.GetEntityWithCarrierEntityId(player.EntityId);
                if (!product.isInboundProduct || product.isInStock ||
                    !product.hasDeliveryEntityId)
                    continue;
                if (product.hasDeliverySlotIndex)
                    throw new InvalidOperationException(
                        $"Carried inbound product {product.EntityId} still occupies a delivery slot.");
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

                HashSet<int> occupiedSlots = GetOrCreateOccupiedSlots(
                    occupiedSlotsByZone,
                    storageZone.EntityId);
                int slotIndex = FindFreeSlot(storageZone.Slots.Length, occupiedSlots);
                if (slotIndex < 0)
                {
                    _events.EmitNotification("На складе нет свободного места");
                    continue;
                }

                player.isHandsOccupied = false;
                product.RemoveCarrierEntityId();
                product.isInboundProduct = false;
                product.isInStock = true;
                product.isInteractable = true;
                product.AddStorageZoneEntityId(storageZone.EntityId);
                product.AddStorageSlotIndex(slotIndex);
                product.isProductPlacementDirty = true;
                product.isProductStocked = true;
                occupiedSlots.Add(slotIndex);
            }
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
