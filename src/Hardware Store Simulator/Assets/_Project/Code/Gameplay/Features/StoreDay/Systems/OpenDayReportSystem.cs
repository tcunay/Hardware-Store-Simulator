using System;
using Entitas;
using HardwareStore.Gameplay.Common.Cursor;
using HardwareStore.Gameplay.Components;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.StoreDay.Systems
{
    public sealed class OpenDayReportSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly ICursorService _cursor;
        private readonly IGroup<GameEntity> _requests;
        private readonly IGroup<GameEntity> _warehouseTasks;

        public OpenDayReportSystem(GameContext gameContext, ICursorService cursor)
        {
            _gameContext = gameContext;
            _cursor = cursor;
            _requests = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.InteractionRequest,
                GameMatcher.SourceEntityId,
                GameMatcher.TargetEntityId));
            _warehouseTasks = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.WarehouseTaskStoreEntityId,
                    GameMatcher.WarehouseTaskStep)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity request in _requests)
            {
                GameEntity terminal =
                    _gameContext.GetEntityWithEntityId(request.TargetEntityId);
                if (terminal == null)
                    throw new InvalidOperationException(
                        $"Store interaction targets missing entity {request.TargetEntityId}.");
                if (!terminal.isStoreControlTerminal)
                    continue;
                if (!terminal.hasEntityId || !terminal.hasStoreEntityId ||
                    !terminal.isInteractable)
                {
                    throw new InvalidOperationException(
                        "Store control terminal has incomplete configuration.");
                }

                GameEntity store = _gameContext.GetEntityWithEntityId(terminal.StoreEntityId);
                if (store == null || !store.isStore || !store.hasEntityId ||
                    !store.hasStoreControlTerminalEntityId ||
                    store.StoreControlTerminalEntityId != terminal.EntityId)
                {
                    throw new InvalidOperationException(
                        $"Store control terminal {terminal.EntityId} has an invalid store relation.");
                }
                int phaseCount =
                    (store.isStorePreparing ? 1 : 0) +
                    (store.isStoreOpen ? 1 : 0) +
                    (store.isStoreClosing ? 1 : 0) +
                    (store.isDayReportOpen ? 1 : 0);
                if (phaseCount != 1)
                {
                    throw new InvalidOperationException(
                        $"Store {store.EntityId} must have exactly one day-cycle phase.");
                }
                if (!store.isStoreClosing)
                    continue;
                if (store.hasCustomerCooldownRemaining)
                {
                    throw new InvalidOperationException(
                        $"Closing store {store.EntityId} cannot schedule another customer.");
                }
                if (StoreDayCustomerVisitGuard.CountActiveVisits(
                        _gameContext,
                        store.EntityId) != 0)
                    continue;
                if (HasActiveWarehouseWork(store))
                    continue;

                GameEntity player =
                    _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                if (player == null || !player.isPlayer || !player.hasEntityId ||
                    !player.hasStoreEntityId || !player.hasMoveDirection ||
                    player.StoreEntityId != store.EntityId)
                {
                    throw new InvalidOperationException(
                        $"Interaction source cannot open report for store {store.EntityId}.");
                }
                if (player.isModalOpen || player.isHandsOccupied)
                    continue;
                if (player.hasConsultationVisitEntityId ||
                    player.hasProcurementTerminalEntityId ||
                    player.hasDayReportStoreEntityId)
                {
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} has a modal relation without ModalOpen.");
                }

                store.isStoreClosing = false;
                store.isDayReportOpen = true;
                player.AddDayReportStoreEntityId(store.EntityId);
                player.isModalOpen = true;
                player.ReplaceMoveDirection(Vector3.zero);
                if (player.hasInteractionPrompt)
                    player.RemoveInteractionPrompt();
                player.isFocusInteractionAvailable = false;
                if (player.hasFocusedInteractionType)
                    player.RemoveFocusedInteractionType();
                if (player.hasFocusedEntityId)
                    player.RemoveFocusedEntityId();
                terminal.isHighlighted = false;
                player.isCursorLocked = true;
                _cursor.SetLocked(true);
            }
        }

        private bool HasActiveWarehouseWork(GameEntity store)
        {
            foreach (GameEntity task in _warehouseTasks)
            {
                if (task.WarehouseTaskStoreEntityId == store.EntityId &&
                    task.WarehouseTaskStep != WarehouseTaskStepId.Blocked &&
                    (task.hasAssignedWorkerEntityId ||
                     task.hasWarehouseTaskReservedStorageSlotIndex))
                {
                    return true;
                }
            }

            GameEntity worker =
                _gameContext.GetEntityWithWarehouseWorkerStoreEntityId(store.EntityId);
            if (worker == null)
                return false;
            if (worker.isDestructed || !worker.isWarehouseWorker ||
                !worker.hasEntityId || !worker.hasWarehouseWorkerStoreEntityId ||
                worker.WarehouseWorkerStoreEntityId != store.EntityId)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} references an invalid warehouse worker.");
            }

            return worker.isHandsOccupied || worker.isCarryingProduct ||
                   _gameContext.GetEntityWithCarrierEntityId(worker.EntityId) != null;
        }
    }
}
