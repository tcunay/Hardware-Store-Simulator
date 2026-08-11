using System;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveTrolleyUpgradeTerminalPromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IStaticDataService _staticData;
        private readonly IGroup<GameEntity> _players;

        public ResolveTrolleyUpgradeTerminalPromptSystem(
            GameContext gameContext,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _staticData = staticData;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.StoreEntityId,
                GameMatcher.FocusedEntityId,
                GameMatcher.FocusedInteractionType));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                if (player.FocusedInteractionType !=
                    InteractionTypeId.TrolleyUpgradeTerminal)
                    continue;

                GameEntity terminal = _gameContext.GetEntityWithEntityId(
                    player.FocusedEntityId);
                GameEntity store = _gameContext.GetEntityWithEntityId(
                    player.StoreEntityId);
                ValidateTerminal(terminal, store);

                if (player.isHandsOccupied)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            LocalizationKey.PromptFreeHandsForTrolleyUpgrade),
                        false);
                    continue;
                }

                if (_gameContext.GetEntityWithTrolleyStoreEntityId(store.EntityId) != null)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(LocalizationKey.PromptTrolleyPurchased),
                        false);
                    continue;
                }

                PlatformTrolleyConfig config = _staticData.PlatformTrolley;
                if (!store.isTrolleyUpgradeUnlocked)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            LocalizationKey.PromptTrolleyUpgradeLocked,
                            store.CompletedOrderCount,
                            config.RequiredCompletedOrderCount),
                        false);
                    continue;
                }

                bool affordable = store.Money >= config.PurchasePrice;
                player.SetInteractionPrompt(
                    affordable
                        ? LocalizedTexts.Text(
                            LocalizationKey.PromptPurchaseTrolley,
                            config.PurchasePrice)
                        : LocalizedTexts.Text(
                            LocalizationKey.PromptTrolleyInsufficientMoney,
                            config.PurchasePrice),
                    affordable);
            }
        }

        private static void ValidateTerminal(GameEntity terminal, GameEntity store)
        {
            if (terminal == null || !terminal.isTrolleyUpgradeTerminal ||
                !terminal.hasEntityId || !terminal.hasStoreEntityId ||
                terminal.StoreEntityId != store.EntityId ||
                !store.isStore || !store.hasEntityId || !store.hasMoney ||
                !store.hasCompletedOrderCount ||
                !store.hasTrolleyUpgradeTerminalEntityId ||
                store.TrolleyUpgradeTerminalEntityId != terminal.EntityId)
            {
                throw new InvalidOperationException(
                    "Trolley upgrade terminal has invalid store configuration.");
            }
        }
    }
}
