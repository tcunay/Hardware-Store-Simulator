using System;
using HardwareStore.Gameplay.Localization;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    internal static class InteractionPromptSystemExtensions
    {
        public static void SetInteractionPrompt(
            this GameEntity player,
            LocalizedText prompt,
            bool available)
        {
            player.ReplaceInteractionPrompt(prompt);
            player.isFocusInteractionAvailable = available;
        }

        public static GameEntity GetCurrentLoadingVisit(
            this GameContext gameContext,
            int storeEntityId)
        {
            GameEntity loadingBay =
                gameContext.GetEntityWithCustomerLoadingBayStoreEntityId(storeEntityId);
            if (loadingBay == null || loadingBay.isDestructed ||
                !loadingBay.isCustomerLoadingBay || !loadingBay.hasEntityId ||
                !loadingBay.hasCustomerLoadingBayStoreEntityId ||
                loadingBay.CustomerLoadingBayStoreEntityId != storeEntityId)
            {
                throw new InvalidOperationException(
                    $"Store {storeEntityId} has an invalid customer loading bay relation.");
            }

            GameEntity visit = gameContext.GetEntityWithReservedCustomerLoadingBayEntityId(
                loadingBay.EntityId);
            if (visit == null)
                return null;

            ValidateLoadingBayReservation(visit, loadingBay, storeEntityId);
            if (!visit.isCustomerVisitLoading)
                return null;
            if (!visit.isLoadingZone || !visit.isOrder ||
                !visit.hasStorageZoneEntityId || !visit.hasSlots)
            {
                throw new InvalidOperationException(
                    $"Loading customer visit {visit.EntityId} has incomplete order state.");
            }

            return visit;
        }

        public static int CountActiveCustomerVisits(
            this GameContext gameContext,
            int storeEntityId)
        {
            int count = 0;
            foreach (GameEntity visit in
                     gameContext.GetEntitiesWithCustomerVisitStoreEntityId(storeEntityId))
            {
                ValidateCustomerVisit(visit, storeEntityId);
                count++;
            }

            return count;
        }

        public static int CountQueuedCustomerVisits(
            this GameContext gameContext,
            int storeEntityId)
        {
            int count = 0;
            foreach (GameEntity visit in
                     gameContext.GetEntitiesWithCustomerVisitStoreEntityId(storeEntityId))
            {
                ValidateCustomerVisit(visit, storeEntityId);
                if (visit.isCustomerVisitQueued)
                    count++;
            }

            return count;
        }

        private static void ValidateLoadingBayReservation(
            GameEntity visit,
            GameEntity loadingBay,
            int storeEntityId)
        {
            ValidateCustomerVisit(visit, storeEntityId);
            if (!visit.hasReservedCustomerLoadingBayEntityId ||
                visit.ReservedCustomerLoadingBayEntityId != loadingBay.EntityId)
            {
                throw new InvalidOperationException(
                    $"Customer loading bay {loadingBay.EntityId} reserves invalid visit " +
                    $"{visit.EntityId}.");
            }
        }

        private static void ValidateCustomerVisit(GameEntity visit, int storeEntityId)
        {
            if (visit == null || visit.isDestructed || !visit.isCustomerVisit ||
                !visit.isCustomerVehicle || !visit.hasEntityId ||
                !visit.hasCustomerVisitStoreEntityId ||
                visit.CustomerVisitStoreEntityId != storeEntityId)
            {
                throw new InvalidOperationException(
                    $"Store {storeEntityId} has an invalid customer visit relation.");
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
