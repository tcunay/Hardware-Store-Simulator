using System;
using Entitas;
using HardwareStore.Gameplay.Common;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Features.Trolley.Systems
{
    public sealed class LoadHeldProductOnTrolleySystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public LoadHeldProductOnTrolleySystem(GameContext gameContext,
            IGameEventFactory events)
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
                GameEntity trolley =
                    _gameContext.GetEntityWithEntityId(request.TargetEntityId);
                if (!trolley.isPlatformTrolley)
                    continue;

                GameEntity player =
                    _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                if (!player.isPlayer || !player.hasEntityId ||
                    !player.isCarryingProduct)
                {
                    continue;
                }

                ValidatePlayer(player, trolley);
                GameEntity product =
                    _gameContext.GetEntityWithCarrierEntityId(player.EntityId);
                ValidateProduct(product, player, trolley);
                int freeSlotIndex = FindFreeSlot(trolley);
                if (freeSlotIndex < 0)
                {
                    _events.EmitNotification(LocalizedTexts.Text(
                        LocalizationKey.NotificationTrolleyFull));
                    continue;
                }

                product.RemoveCarrierEntityId();
                product.AddTrolleyEntityId(trolley.EntityId);
                product.AddTrolleySlotIndex(freeSlotIndex);
                product.isInteractable = true;
                product.isProductPlacementDirty = true;
                player.isCarryingProduct = false;
                player.isHandsOccupied = false;
                _events.EmitAudio(AudioCueId.Load);
            }
        }

        private int FindFreeSlot(GameEntity trolley)
        {
            if (!trolley.hasEntityId || !trolley.hasTrolleyStoreEntityId ||
                !trolley.hasTrolleyCapacity ||
                !trolley.hasOccupiedTrolleySlotCount || !trolley.hasSlots ||
                trolley.TrolleyCapacity <= 0 ||
                trolley.Slots.Length != trolley.TrolleyCapacity ||
                trolley.hasTrolleyPusherEntityId || !trolley.isInteractable ||
                trolley.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Platform trolley {trolley.EntityId} cannot receive cargo.");
            }

            var occupiedSlots = new bool[trolley.TrolleyCapacity];
            foreach (GameEntity product in
                     _gameContext.GetEntitiesWithTrolleyEntityId(trolley.EntityId))
            {
                if (product.isDestructed)
                    continue;
                if (!product.isProduct || !product.hasEntityId ||
                    !product.hasTrolleySlotIndex ||
                    product.TrolleySlotIndex < 0 ||
                    product.TrolleySlotIndex >= occupiedSlots.Length ||
                    occupiedSlots[product.TrolleySlotIndex])
                {
                    throw new InvalidOperationException(
                        $"Trolley {trolley.EntityId} contains invalid slot ownership.");
                }

                occupiedSlots[product.TrolleySlotIndex] = true;
            }

            for (int index = 0; index < occupiedSlots.Length; index++)
            {
                if (!occupiedSlots[index])
                    return index;
            }

            return -1;
        }

        private static void ValidatePlayer(GameEntity player, GameEntity trolley)
        {
            if (!player.hasStoreEntityId ||
                player.StoreEntityId != trolley.TrolleyStoreEntityId ||
                !player.isHandsOccupied || player.isPushingTrolley ||
                player.isModalOpen)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} cannot load trolley {trolley.EntityId}.");
            }
        }

        private void ValidateProduct(
            GameEntity product,
            GameEntity player,
            GameEntity trolley)
        {
            if (product != null && product.isInboundProduct)
                InboundProductManifestValidator.Validate(_gameContext, product);

            bool hasInboundReservation = product != null &&
                product.isInboundProduct && !product.isInStock &&
                product.hasDeliveryEntityId &&
                product.hasPurchaseOrderLineEntityId &&
                product.hasReservedDeliverySlotIndex &&
                !product.hasReservedStorageSlotIndex &&
                !product.hasReservedOrderLineEntityId;
            bool hasStockReservation = product != null &&
                product.isInStock && !product.isInboundProduct &&
                product.hasStorageZoneEntityId &&
                product.hasReservedStorageSlotIndex &&
                product.hasReservedOrderLineEntityId &&
                !product.hasReservedDeliverySlotIndex;
            if (product == null || !product.isProduct || !product.hasEntityId ||
                !product.hasCarrierEntityId ||
                product.CarrierEntityId != player.EntityId ||
                (!hasInboundReservation && !hasStockReservation) ||
                product.hasTrolleyEntityId || product.hasTrolleySlotIndex ||
                product.hasDeliverySlotIndex || product.hasStorageSlotIndex ||
                product.hasOrderLineEntityId || product.hasLoadingSlotIndex ||
                product.isLooseProduct || product.isLoaded ||
                product.hasWorldPosition || product.hasWorldRotation ||
                product.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} carries a product that cannot be put on a trolley.");
            }

            if (product.isInboundProduct)
            {
                if (!product.hasDeliveryEntityId ||
                    !product.hasPurchaseOrderLineEntityId ||
                    !product.hasReservedDeliverySlotIndex || product.isInStock ||
                    product.hasStorageZoneEntityId ||
                    product.hasReservedStorageSlotIndex ||
                    product.hasReservedOrderLineEntityId)
                {
                    throw new InvalidOperationException(
                        $"Inbound product {product.EntityId} has invalid trolley reservation state.");
                }

                GameEntity delivery = _gameContext.GetEntityWithEntityId(
                    product.DeliveryEntityId);
                if (delivery == null || !delivery.isDeliveryActive ||
                    !delivery.hasStoreEntityId || !delivery.hasSlots ||
                    delivery.StoreEntityId != trolley.TrolleyStoreEntityId ||
                    product.ReservedDeliverySlotIndex < 0 ||
                    product.ReservedDeliverySlotIndex >= delivery.Slots.Length)
                {
                    throw new InvalidOperationException(
                        $"Inbound product {product.EntityId} belongs to another store.");
                }

                return;
            }

            if (!product.isInStock || !product.hasStorageZoneEntityId ||
                !product.hasReservedStorageSlotIndex ||
                !product.hasReservedOrderLineEntityId ||
                product.hasDeliveryEntityId || product.hasReservedDeliverySlotIndex)
            {
                throw new InvalidOperationException(
                    $"Stock product {product.EntityId} has invalid trolley reservation state.");
            }

            GameEntity store = _gameContext.GetEntityWithEntityId(
                trolley.TrolleyStoreEntityId);
            if (store == null || !store.isStore || !store.hasStorageZoneEntityId ||
                store.StorageZoneEntityId != product.StorageZoneEntityId)
            {
                throw new InvalidOperationException(
                    $"Stock product {product.EntityId} belongs to another store.");
            }

            GameEntity orderLine = _gameContext.GetEntityWithEntityId(
                product.ReservedOrderLineEntityId);
            if (orderLine == null || !orderLine.isOrderLine || orderLine.isDestructed ||
                !orderLine.hasEntityId ||
                orderLine.EntityId != product.ReservedOrderLineEntityId ||
                !orderLine.hasProductType ||
                orderLine.ProductType != product.ProductType)
            {
                throw new InvalidOperationException(
                    $"Stock product {product.EntityId} has an invalid order reservation.");
            }
        }
    }
}
