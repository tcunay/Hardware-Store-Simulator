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

                if (!player.hasFocusedEntityId || !player.hasFocusedInteractionType ||
                    player.FocusedInteractionType != InteractionTypeId.PlatformTrolley)
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
                    true);
            }
        }

        private static void ValidateTrolley(GameEntity player, GameEntity trolley)
        {
            if (trolley == null || !trolley.isPlatformTrolley ||
                !trolley.hasEntityId || !trolley.hasTrolleyStoreEntityId ||
                trolley.TrolleyStoreEntityId != player.StoreEntityId ||
                !trolley.hasTrolleyCapacity ||
                !trolley.hasOccupiedTrolleySlotCount || trolley.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} focuses an invalid platform trolley.");
            }
        }
    }
}
