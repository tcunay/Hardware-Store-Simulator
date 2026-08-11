using System;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveProcurementTerminalPromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;

        public ResolveProcurementTerminalPromptSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
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
                if (player.FocusedInteractionType != InteractionTypeId.ProcurementTerminal)
                    continue;

                GameEntity terminal =
                    _gameContext.GetEntityWithEntityId(player.FocusedEntityId);
                if (terminal.StoreEntityId != player.StoreEntityId)
                    continue;
                if (!terminal.hasSelectedProductType)
                    throw new InvalidOperationException(
                        $"Procurement terminal {terminal.EntityId} has no selected product type.");

                if (player.isHandsOccupied)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            player.isPushingTrolley
                                ? LocalizationKey.PromptReleaseTrolleyFirst
                                : LocalizationKey.PromptFreeHandsForProcurement),
                        false);
                    continue;
                }

                GameEntity delivery =
                    _gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                        terminal.EntityId);
                if (delivery != null)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            LocalizationKey.PromptDeliveryBeingStocked,
                            LocalizedTexts.ProductName(delivery.ProductType),
                            delivery.StockedProductCount,
                            delivery.DeliveryProductCount,
                            LocalizedTexts.ProductUnit(delivery.ProductType)),
                        false);
                    continue;
                }

                player.SetInteractionPrompt(
                    LocalizedTexts.Text(LocalizationKey.PromptOpenProcurement),
                    true);
            }
        }
    }
}
