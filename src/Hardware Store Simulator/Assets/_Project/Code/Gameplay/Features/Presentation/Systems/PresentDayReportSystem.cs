using System;
using Entitas;
using HardwareStore.Gameplay.Presentation;

namespace HardwareStore.Gameplay.Features.Presentation.Systems
{
    public sealed class PresentDayReportSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IHudService _hud;
        private readonly IGroup<GameEntity> _players;

        public PresentDayReportSystem(GameContext gameContext, IHudService hud)
        {
            _gameContext = gameContext;
            _hud = hud;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Player,
                    GameMatcher.EntityId,
                    GameMatcher.StoreEntityId)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            _hud.PresentDayReport(null);

            GameEntity reportPlayer = null;
            foreach (GameEntity player in _players)
            {
                if (!player.hasDayReportStoreEntityId)
                    continue;
                if (reportPlayer != null)
                {
                    throw new InvalidOperationException(
                        "The local HUD cannot present more than one day report at a time.");
                }

                reportPlayer = player;
            }

            if (reportPlayer != null)
                Present(reportPlayer);
        }

        private void Present(GameEntity player)
        {
            GameEntity playerStore = _gameContext.GetEntityWithEntityId(
                player.StoreEntityId);
            if (playerStore == null || !playerStore.isStore || playerStore.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} references an invalid store.");
            }

            if (player.DayReportStoreEntityId != playerStore.EntityId ||
                !playerStore.isDayReportOpen || !player.isModalOpen)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} has an invalid day report relation.");
            }
            if (!playerStore.hasDayNumber || !playerStore.hasDayOpeningBalance ||
                !playerStore.hasDayRevenue || !playerStore.hasDayProcurementExpenses ||
                !playerStore.hasDayUpgradeExpenses ||
                !playerStore.hasDayPayrollExpenses ||
                !playerStore.hasDayCompletedOrderCount || !playerStore.hasMoney ||
                !playerStore.hasStorageZoneEntityId)
            {
                throw new InvalidOperationException(
                    $"Store {playerStore.EntityId} cannot build a complete day report.");
            }

            GameEntity storage = _gameContext.GetEntityWithEntityId(
                playerStore.StorageZoneEntityId);
            if (storage == null || storage.isDestructed || !storage.isStorageZone ||
                !storage.hasStorageProductCount)
            {
                throw new InvalidOperationException(
                    $"Store {playerStore.EntityId} has invalid report storage.");
            }

            _hud.PresentDayReport(new DayReportSnapshot(
                playerStore.DayNumber,
                playerStore.DayOpeningBalance,
                playerStore.DayRevenue,
                playerStore.DayProcurementExpenses,
                playerStore.DayUpgradeExpenses,
                playerStore.DayPayrollExpenses,
                playerStore.Money,
                playerStore.DayCompletedOrderCount,
                storage.StorageProductCount));
        }
    }
}
