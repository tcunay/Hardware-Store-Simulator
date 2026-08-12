using System;
using Entitas;
using HardwareStore.Gameplay.Common.Economy;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveStoreControlTerminalPromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly WarehouseWorkerConfig _workerConfig;
        private readonly IEconomySolvencyService _solvency;
        private readonly IGroup<GameEntity> _players;

        public ResolveStoreControlTerminalPromptSystem(
            GameContext gameContext,
            IStaticDataService staticData,
            IEconomySolvencyService solvency)
        {
            _gameContext = gameContext;
            _workerConfig = staticData.WarehouseWorker;
            _solvency = solvency;
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
                !store.hasDayNumber || !store.hasMoney ||
                !store.hasCompletedOrderCount ||
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
                ResolveOpenStorePrompt(player, store);
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

            GameEntity worker =
                _gameContext.GetEntityWithWarehouseWorkerStoreEntityId(store.EntityId);
            if (worker != null)
            {
                ValidateWorker(worker, store);
                GameEntity workerTask =
                    _gameContext.GetEntityWithAssignedWorkerEntityId(worker.EntityId);
                if (workerTask != null || worker.isHandsOccupied ||
                    worker.isCarryingProduct)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            LocalizationKey.PromptCloseStoreWarehouseWorkerBusy),
                        false);
                    return;
                }
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

        private void ResolveOpenStorePrompt(GameEntity player, GameEntity store)
        {
            GameEntity worker =
                _gameContext.GetEntityWithWarehouseWorkerStoreEntityId(store.EntityId);
            if (worker == null)
            {
                if (!store.isWarehouseWorkerHiringUnlocked)
                {
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(
                            LocalizationKey.PromptWarehouseWorkerLocked,
                            _workerConfig.RequiredCompletedOrderCount,
                            store.CompletedOrderCount),
                        false);
                    return;
                }

                ResolveDebitPrompt(
                    player,
                    store,
                    _workerConfig.HirePrice,
                    LocalizationKey.PromptHireWarehouseWorker,
                    LocalizationKey.PromptWarehouseWorkerHireInsufficientMoney,
                    LocalizationKey.PromptWarehouseWorkerHireWouldBlockProjects,
                    _workerConfig.HirePrice,
                    _workerConfig.DailyWage);
                return;
            }

            ValidateWorker(worker, store);
            if (worker.isWorkerShiftActive)
            {
                if (worker.WorkerPaidDayNumber != store.DayNumber)
                {
                    throw new InvalidOperationException(
                        $"Warehouse worker {worker.EntityId} has an active unpaid shift.");
                }

                player.SetInteractionPrompt(
                    LocalizedTexts.Text(
                        LocalizationKey.PromptWarehouseWorkerActive,
                        _workerConfig.DailyWage),
                    false);
                return;
            }

            if (worker.WorkerPaidDayNumber >= store.DayNumber)
            {
                throw new InvalidOperationException(
                    $"Warehouse worker {worker.EntityId} has a paid inactive shift for day " +
                    $"{store.DayNumber}.");
            }

            ResolveDebitPrompt(
                player,
                store,
                _workerConfig.DailyWage,
                LocalizationKey.PromptPayWarehouseWorkerShift,
                LocalizationKey.PromptWarehouseWorkerWageInsufficientMoney,
                LocalizationKey.PromptWarehouseWorkerWageWouldBlockProjects,
                _workerConfig.DailyWage);
        }

        private void ResolveDebitPrompt(
            GameEntity player,
            GameEntity store,
            int amount,
            LocalizationKey availableKey,
            LocalizationKey insufficientMoneyKey,
            LocalizationKey unsafeKey,
            params LocalizationArgument[] availableArguments)
        {
            EconomyDebitEvaluation debit = _solvency.EvaluateDebit(
                store.EntityId,
                amount);
            switch (debit.Availability)
            {
                case EconomyDebitAvailability.Available:
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(availableKey, availableArguments),
                        true);
                    break;
                case EconomyDebitAvailability.InsufficientMoney:
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(insufficientMoneyKey, amount),
                        false);
                    break;
                case EconomyDebitAvailability.DemandWouldBecomeInsolvent:
                    player.SetInteractionPrompt(
                        LocalizedTexts.Text(unsafeKey),
                        false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void ValidateWorker(GameEntity worker, GameEntity store)
        {
            if (worker.isDestructed || !worker.isWarehouseWorker || !worker.hasEntityId ||
                !worker.hasWarehouseWorkerStoreEntityId ||
                worker.WarehouseWorkerStoreEntityId != store.EntityId ||
                !worker.hasWorkerPaidDayNumber || !worker.hasWarehouseWorkerStatus)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} references an invalid warehouse worker.");
            }
        }
    }
}
