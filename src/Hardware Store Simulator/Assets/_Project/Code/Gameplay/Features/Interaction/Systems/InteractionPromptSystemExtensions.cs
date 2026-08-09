namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    internal static class InteractionPromptSystemExtensions
    {
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
