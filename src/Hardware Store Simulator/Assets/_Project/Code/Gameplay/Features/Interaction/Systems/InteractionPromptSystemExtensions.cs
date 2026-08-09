using System;
using HardwareStore.Common.Entity;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    internal static class InteractionPromptSystemExtensions
    {
        public static GameEntity RequireStore(
            this GameContext gameContext,
            GameEntity player)
        {
            GameEntity store = gameContext.GetRequiredEntity(
                player.StoreEntityId,
                "player store");
            if (!store.isStore)
                throw new InvalidOperationException(
                    $"Entity {player.StoreEntityId} is not a store.");

            return store;
        }

        public static void SetInteractionPrompt(
            this GameEntity player,
            string prompt,
            bool available)
        {
            player.ReplaceInteractionPrompt(prompt);
            player.isFocusInteractionAvailable = available;
        }
    }
}
