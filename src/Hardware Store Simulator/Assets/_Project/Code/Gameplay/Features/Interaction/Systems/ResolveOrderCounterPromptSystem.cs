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
                if (orderCounter == null || orderCounter.isDestructed ||
                    !orderCounter.isOrderCounter || !orderCounter.isInteractable ||
                    !orderCounter.hasEntityId || !orderCounter.hasStoreEntityId)
                {
                    throw new InvalidOperationException(
                        $"Player focuses invalid order counter {player.FocusedEntityId}.");
                }
                if (orderCounter.StoreEntityId != player.StoreEntityId)
                    continue;

                GameEntity customerVisit =
                    _gameContext.GetEntityWithServingOrderCounterEntityId(
                        orderCounter.EntityId);
                if (customerVisit == null)
                {
                    int queuedCustomerCount =
                        _gameContext.CountQueuedCustomerVisits(
                            orderCounter.StoreEntityId);
                    player.SetInteractionPrompt(
                        queuedCustomerCount > 0
                            ? LocalizedTexts.Text(
                                LocalizationKey.PromptCounterNextCustomerApproaching,
                                queuedCustomerCount)
                            : LocalizedTexts.Text(ResolveNoCustomerPrompt(
                                orderCounter.StoreEntityId)),
                        false);
                    continue;
                }
                ValidateConsultingVisit(customerVisit, orderCounter);
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

        private static void ValidateConsultingVisit(
            GameEntity visit,
            GameEntity orderCounter)
        {
            if (visit.isDestructed || !visit.isCustomerVisit ||
                !visit.isCustomerVehicle || !visit.isCustomerVisitConsulting ||
                visit.isOrder || !visit.hasEntityId ||
                !visit.hasCustomerVisitStoreEntityId ||
                !visit.hasCustomerProjectType ||
                !visit.hasServingOrderCounterEntityId ||
                visit.CustomerVisitStoreEntityId != orderCounter.StoreEntityId ||
                visit.ServingOrderCounterEntityId != orderCounter.EntityId)
            {
                throw new InvalidOperationException(
                    $"Order counter {orderCounter.EntityId} serves invalid customer visit.");
            }

            int lifecycleCount =
                (visit.isCustomerVisitArriving ? 1 : 0) +
                (visit.isCustomerVisitQueued ? 1 : 0) +
                (visit.isCustomerVisitConsulting ? 1 : 0) +
                (visit.isCustomerVisitWaitingForLoadingBay ? 1 : 0) +
                (visit.isCustomerVisitMovingToLoadingBay ? 1 : 0) +
                (visit.isCustomerVisitLoading ? 1 : 0) +
                (visit.isCustomerVisitCompleted ? 1 : 0) +
                (visit.isCustomerVisitReturning ? 1 : 0) +
                (visit.isCustomerVisitDeparting ? 1 : 0) +
                (visit.isCustomerVisitAbandoning ? 1 : 0) +
                (visit.isCustomerVisitWaitingForAbandonDeparture ? 1 : 0) +
                (visit.isCustomerVisitAbandonDeparting ? 1 : 0);
            if (lifecycleCount != 1)
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} must have exactly one lifecycle marker.");
            }
        }

    }
}
