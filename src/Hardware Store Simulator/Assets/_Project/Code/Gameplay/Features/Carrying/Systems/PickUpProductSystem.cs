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

                GameEntity player = _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                if (player.isHandsOccupied)
                    continue;
                if (product.hasCarrierEntityId || product.isLoaded)
                    continue;

                bool canPickUp = product.isInboundProduct
                    ? CanPickUpInbound(product, player)
                    : CanPickUpStock(product, player);
                if (!canPickUp)
                    continue;

                if (product.isInStock)
                    ReleaseStorageSlot(product);

                ReleaseDeliverySlot(product);
                ReleaseLoosePose(product);
                if (product.hasDeliverySlotIndex || product.hasStorageSlotIndex ||
                    product.hasOrderLineEntityId || product.hasLoadingSlotIndex)
                {
                    throw new InvalidOperationException(
                        $"Product {product.EntityId} contains stale slot placement state.");
                }

                product.AddCarrierEntityId(player.EntityId);
                player.isHandsOccupied = true;
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
            if (!delivery.isDeliveryActive)
                throw new InvalidOperationException(
                    $"Inbound product {product.EntityId} is linked to an inactive delivery.");

            return delivery.StoreEntityId == player.StoreEntityId;
        }

        private bool CanPickUpStock(GameEntity product, GameEntity player)
        {
            if (!product.isInStock)
                return false;
            if (!product.hasStorageZoneEntityId)
                throw new InvalidOperationException(
                    $"In-stock product {product.EntityId} has no storage ownership relation.");

            GameEntity store = _gameContext.GetEntityWithEntityId(player.StoreEntityId);
            if (product.StorageZoneEntityId != store.StorageZoneEntityId)
                return false;
            GameEntity customerVisit =
                _gameContext.GetEntityWithCustomerVisitStoreEntityId(store.EntityId);
            if (customerVisit == null)
                return false;
            if (!customerVisit.isCustomerVisitLoading)
                return false;
            if (!customerVisit.isOrder || !customerVisit.hasEntityId)
                throw new InvalidOperationException(
                    $"Loading customer visit {customerVisit.EntityId} has no order.");

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

            return matchingLine != null &&
                   matchingLine.LoadedProductCount < matchingLine.RequiredProductCount;
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

        private static void ReleaseDeliverySlot(GameEntity product)
        {
            if (product.hasDeliverySlotIndex)
                product.RemoveDeliverySlotIndex();
        }

        private static void ReleaseStorageSlot(GameEntity product)
        {
            if (!product.hasStorageZoneEntityId)
                throw new InvalidOperationException(
                    $"In-stock product {product.EntityId} has no storage ownership relation.");

            if (product.hasStorageSlotIndex)
                product.RemoveStorageSlotIndex();
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
    }
}
