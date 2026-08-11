using System;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolvePlatformTrolleyPromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;

        public ResolvePlatformTrolleyPromptSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.EntityId,
                GameMatcher.StoreEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                if (player.isPushingTrolley)
                {
                    GameEntity pushedTrolley =
                        _gameContext.GetEntityWithTrolleyPusherEntityId(player.EntityId);
                    if (pushedTrolley == null || !pushedTrolley.isPlatformTrolley)
                        throw new InvalidOperationException(
                            $"Player {player.EntityId} has no pushed trolley relation.");

                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(LocalizationKey.PromptReleaseTrolley),
                        false);
                    continue;
                }

                if (!player.hasFocusedEntityId || !player.hasFocusedInteractionType)
                    continue;

                if (player.FocusedInteractionType == InteractionTypeId.Product)
                {
                    ResolveProductTrolleyPrompt(player);
                    continue;
                }

                if (player.FocusedInteractionType != InteractionTypeId.PlatformTrolley)
                    continue;

                GameEntity trolley =
                    _gameContext.GetEntityWithEntityId(player.FocusedEntityId);
                ValidateTrolley(player, trolley);

                if (trolley.hasTrolleyPusherEntityId)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            LocalizationKey.PromptTrolleyPushedByOther),
                        false);
                    continue;
                }

                if (player.isCarryingProduct)
                {
                    GameEntity product =
                        _gameContext.GetEntityWithCarrierEntityId(player.EntityId);
                    if (product == null || !product.isProduct ||
                        product.isDestructed)
                    {
                        throw new InvalidOperationException(
                            $"Player {player.EntityId} has invalid carried-product state.");
                    }

                    bool isFull = trolley.OccupiedTrolleySlotCount >=
                                  trolley.TrolleyCapacity;
                    player.SetInteractionPrompt(
                        isFull
                            ? LocalizedTexts.Text(
                                LocalizationKey.PromptTrolleyFull,
                                trolley.OccupiedTrolleySlotCount,
                                trolley.TrolleyCapacity)
                            : LocalizedTexts.Text(
                                LocalizationKey.PromptPlaceProductOnTrolley,
                                LocalizedTexts.ProductName(product.ProductType),
                                trolley.OccupiedTrolleySlotCount,
                                trolley.TrolleyCapacity),
                        !isFull);
                    continue;
                }

                if (player.isHandsOccupied)
                {
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} has an unknown hand occupancy role.");
                }

                player.SetInteractionPrompt(
                    LocalizedTexts.Text(
                        LocalizationKey.PromptPushTrolley,
                        trolley.OccupiedTrolleySlotCount,
                        trolley.TrolleyCapacity),
                    false);
            }
        }

        private void ResolveProductTrolleyPrompt(GameEntity player)
        {
            GameEntity product =
                _gameContext.GetEntityWithEntityId(player.FocusedEntityId);
            if (product == null || !product.isProduct || !product.hasEntityId ||
                product.EntityId != player.FocusedEntityId ||
                !product.hasProductType || product.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} focuses an invalid product.");
            }

            if (!product.hasTrolleyEntityId)
            {
                if (product.hasTrolleySlotIndex)
                {
                    throw new InvalidOperationException(
                        $"Product {product.EntityId} has a trolley slot without a " +
                        "trolley relation.");
                }

                return;
            }

            if (!product.isInteractable)
            {
                throw new InvalidOperationException(
                    $"Trolley product {product.EntityId} is not interactable.");
            }

            if (!product.hasTrolleySlotIndex)
            {
                throw new InvalidOperationException(
                    $"Product {product.EntityId} has no trolley slot for its trolley " +
                    "relation.");
            }

            GameEntity trolley =
                _gameContext.GetEntityWithEntityId(product.TrolleyEntityId);
            ValidateTrolley(player, trolley);
            if (!_gameContext.GetEntitiesWithTrolleyEntityId(trolley.EntityId)
                    .Contains(product) ||
                product.TrolleySlotIndex < 0 ||
                product.TrolleySlotIndex >= trolley.TrolleyCapacity)
            {
                throw new InvalidOperationException(
                    $"Product {product.EntityId} has an invalid indexed relation to " +
                    $"trolley {trolley.EntityId}.");
            }

            if (player.isHandsOccupied || trolley.hasTrolleyPusherEntityId)
                return;

            if (!player.hasInteractionPrompt)
            {
                throw new InvalidOperationException(
                    $"Focused trolley product {product.EntityId} has no product prompt.");
            }

            LocalizedText productPrompt = player.InteractionPrompt;
            bool productActionAvailable = player.isFocusInteractionAvailable;
            player.SetInteractionPrompt(
                LocalizedTexts.Text(
                    LocalizationKey.PromptProductAndTrolleyActions,
                    productPrompt),
                productActionAvailable);
        }

        private static void ValidateTrolley(GameEntity player, GameEntity trolley)
        {
            if (trolley == null || !trolley.isPlatformTrolley ||
                !trolley.hasEntityId || !trolley.hasTrolleyStoreEntityId ||
                trolley.TrolleyStoreEntityId != player.StoreEntityId ||
                !trolley.hasTrolleyCapacity ||
                !trolley.hasOccupiedTrolleySlotCount || !trolley.hasSlots ||
                trolley.TrolleyCapacity <= 0 ||
                trolley.Slots.Length != trolley.TrolleyCapacity ||
                trolley.OccupiedTrolleySlotCount < 0 ||
                trolley.OccupiedTrolleySlotCount > trolley.TrolleyCapacity ||
                (!trolley.hasTrolleyPusherEntityId && !trolley.isInteractable) ||
                (trolley.hasTrolleyPusherEntityId && trolley.isInteractable) ||
                trolley.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} focuses an invalid platform trolley.");
            }
        }
    }
}
