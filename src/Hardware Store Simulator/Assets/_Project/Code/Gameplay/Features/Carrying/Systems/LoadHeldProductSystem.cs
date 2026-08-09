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
                if (!visit.isLoadingZone)
                    continue;
                if (!visit.isCustomerVisitLoading)
                    continue;

                GameEntity player = _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                if (player.StoreEntityId != visit.CustomerVisitStoreEntityId ||
                    !player.isHandsOccupied)
                    continue;
                if (visit.LoadedProductCount >= visit.RequiredProductCount)
                    continue;

                GameEntity product = _gameContext.GetEntityWithCarrierEntityId(player.EntityId);
                if (!product.isInStock || product.isLoaded ||
                    product.ProductType != visit.RequiredProductType)
                    continue;
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
                if (product.isLooseProduct || product.hasWorldPosition || product.hasWorldRotation)
                    throw new InvalidOperationException(
                        $"Carried product {product.EntityId} still contains loose placement state.");
                if (product.hasCustomerVisitEntityId || product.hasLoadingSlotIndex)
                    throw new InvalidOperationException(
                        $"Carried product {product.EntityId} already contains customer loading state.");
                if (visit.Slots.Length <= visit.LoadedProductCount)
                    throw new InvalidOperationException(
                        $"Customer visit {visit.EntityId} has insufficient loading slots.");

                product.RemoveCarrierEntityId();
                player.isHandsOccupied = false;
                product.isInStock = false;
                product.isLoaded = true;
                product.isInteractable = false;
                product.RemoveStorageZoneEntityId();
                product.AddCustomerVisitEntityId(visit.EntityId);
                product.AddLoadingSlotIndex(visit.LoadedProductCount);
                product.isProductLoaded = true;
                product.isProductPlacementDirty = true;
            }
        }
    }
}
