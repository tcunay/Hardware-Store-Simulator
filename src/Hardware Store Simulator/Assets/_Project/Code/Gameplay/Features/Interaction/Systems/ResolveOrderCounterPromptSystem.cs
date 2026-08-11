using System;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveOrderCounterPromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;

        public ResolveOrderCounterPromptSystem(GameContext gameContext)
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
                if (player.FocusedInteractionType != InteractionTypeId.OrderCounter)
                    continue;

                GameEntity orderCounter =
                    _gameContext.GetEntityWithEntityId(player.FocusedEntityId);
                if (orderCounter.StoreEntityId != player.StoreEntityId)
                    continue;

                GameEntity customerVisit =
                    _gameContext.GetEntityWithCustomerVisitStoreEntityId(
                        orderCounter.StoreEntityId);
                if (customerVisit == null)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(LocalizationKey.PromptCounterWaitCustomer),
                        false);
                    continue;
                }
                if (customerVisit.isCustomerVisitArriving)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            LocalizationKey.PromptCounterCustomerApproaching),
                        false);
                    continue;
                }

                if (customerVisit.isCustomerVisitDeparting)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(LocalizationKey.PromptCounterVehicleDeparting),
                        false);
                    continue;
                }

                if (customerVisit.isCustomerVisitReturning)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(LocalizationKey.PromptCounterCustomerReturning),
                        false);
                    continue;
                }

                if (customerVisit.isCustomerVisitConsulting)
                {
                    player.SetInteractionPrompt(
                        player.isHandsOccupied
                            ? LocalizedTexts.Text(
                                player.isPushingTrolley
                                    ? LocalizationKey.PromptReleaseTrolleyFirst
                                    : LocalizationKey.PromptFreeHandsForConsultation)
                            : LocalizedTexts.Text(
                                LocalizationKey.PromptDiscussProject,
                                LocalizedTexts.ProjectTitle(
                                    customerVisit.CustomerProjectType)),
                        !player.isHandsOccupied);
                    continue;
                }

                if (customerVisit.isCustomerVisitLoading)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            LocalizationKey.PromptOrderAcceptedLoadVehicle),
                        false);
                    continue;
                }

                if (!customerVisit.isCustomerVisitCompleted)
                    throw new InvalidOperationException(
                        $"Customer visit {customerVisit.EntityId} has no valid lifecycle state.");

                player.SetInteractionPrompt(
                    LocalizedTexts.Text(
                        LocalizationKey.PromptOrderCompletedCustomerLeaving),
                    false);
            }
        }

    }
}
