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
                        LocalizedTexts.Text(ResolveNoCustomerPrompt(
                            orderCounter.StoreEntityId)),
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

        private LocalizationKey ResolveNoCustomerPrompt(int storeEntityId)
        {
            GameEntity store = _gameContext.GetEntityWithEntityId(storeEntityId);
            if (store == null || store.isDestructed || !store.isStore)
            {
                throw new InvalidOperationException(
                    $"Order counter references invalid store {storeEntityId}.");
            }

            if (store.isStorePreparing)
                return LocalizationKey.PromptCounterOpenStoreAtControlTerminal;
            if (store.isStoreOpen)
                return LocalizationKey.PromptCounterWaitCustomer;
            if (store.isStoreClosing)
                return LocalizationKey.PromptCounterFinishDayAtControlTerminal;
            if (store.isDayReportOpen)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} cannot expose an order-counter prompt while its " +
                    "mandatory day report is open.");
            }

            throw new InvalidOperationException(
                $"Store {store.EntityId} has no valid day phase.");
        }

    }
}
