using System;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveStoreControlTerminalPromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly int _closingHour;
        private readonly int _closingMinute;
        private readonly IGroup<GameEntity> _players;

        public ResolveStoreControlTerminalPromptSystem(GameContext gameContext,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            int closingMinute = staticData.StoreDay.ClosingMinute;
            _closingHour = closingMinute / 60;
            _closingMinute = closingMinute % 60;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.EntityId,
                GameMatcher.StoreEntityId,
                GameMatcher.FocusedEntityId,
                GameMatcher.FocusedInteractionType));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                if (player.FocusedInteractionType !=
                    InteractionTypeId.StoreControlTerminal)
                {
                    continue;
                }

                Resolve(player);
            }
        }

        private void Resolve(GameEntity player)
        {
            GameEntity terminal = _gameContext.GetEntityWithEntityId(
                player.FocusedEntityId);
            GameEntity store = _gameContext.GetEntityWithEntityId(player.StoreEntityId);
            if (terminal == null || terminal.isDestructed ||
                !terminal.isStoreControlTerminal || !terminal.hasStoreEntityId ||
                terminal.StoreEntityId != player.StoreEntityId || store == null ||
                store.isDestructed || !store.isStore ||
                !store.hasStoreControlTerminalEntityId ||
                store.StoreControlTerminalEntityId != terminal.EntityId)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} focuses an invalid store control terminal.");
            }

            int phaseCount =
                (store.isStorePreparing ? 1 : 0) +
                (store.isStoreOpen ? 1 : 0) +
                (store.isStoreClosing ? 1 : 0) +
                (store.isDayReportOpen ? 1 : 0);
            if (phaseCount != 1)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} must have exactly one day phase.");
            }

            if (store.isStorePreparing)
            {
                player.SetInteractionPrompt(
                    LocalizedTexts.Text(LocalizationKey.PromptOpenStore),
                    true);
                return;
            }

            if (store.isStoreOpen)
            {
                player.SetInteractionPrompt(
                    LocalizedTexts.Text(
                        LocalizationKey.PromptStoreOpenUntil,
                        _closingHour,
                        _closingMinute),
                    false);
                return;
            }

            if (store.isDayReportOpen)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} focuses the store terminal while its mandatory " +
                    "day report is open.");
            }

            if (_gameContext.GetEntityWithCustomerVisitStoreEntityId(store.EntityId) != null)
            {
                player.SetInteractionPrompt(
                    LocalizedTexts.Text(
                        LocalizationKey.PromptCloseStoreCustomerActive),
                    false);
                return;
            }

            if (player.isHandsOccupied)
            {
                player.SetInteractionPrompt(
                    LocalizedTexts.Text(
                        LocalizationKey.PromptCloseStoreHandsOccupied),
                    false);
                return;
            }

            player.SetInteractionPrompt(
                LocalizedTexts.Text(LocalizationKey.PromptCloseStoreForReport),
                true);
        }
    }
}
