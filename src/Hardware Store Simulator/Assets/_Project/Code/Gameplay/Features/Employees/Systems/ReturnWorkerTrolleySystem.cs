using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Navigation;
using HardwareStore.Gameplay.Common.Physics;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Features.Employees;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class ReturnWorkerTrolleySystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly WarehouseWorkerConfig _config;
        private readonly IWorkerNavigationService _navigation;
        private readonly IWorkerTrolleyHitchService _hitch;
        private readonly IGroup<GameEntity> _workers;
        private readonly List<GameEntity> _buffer = new(1);

        public ReturnWorkerTrolleySystem(GameContext gameContext,
            IStaticDataService staticData, IWorkerNavigationService navigation,
            IWorkerTrolleyHitchService hitch)
        {
            _gameContext = gameContext;
            _config = staticData.WarehouseWorker;
            _navigation = navigation;
            _hitch = hitch;
            _workers = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseWorker, GameMatcher.EntityId,
                    GameMatcher.WarehouseWorkerStoreEntityId,
                    GameMatcher.WarehouseWorkerStatus,
                    GameMatcher.PushingWorkerTrolley,
                    GameMatcher.HandsOccupied,
                    GameMatcher.Transform, GameMatcher.NavigationAgent)
                .NoneOf(
                    GameMatcher.CarryingProduct,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity worker in _workers.GetEntities(_buffer))
            {
                if (worker.WarehouseWorkerStatus !=
                    WarehouseWorkerStatusId.ReturningWorkerTrolley)
                    continue;
                if (_gameContext.GetEntityWithAssignedWorkerEntityId(
                        worker.EntityId) != null)
                    throw new InvalidOperationException(
                        $"Returning worker {worker.EntityId} owns a task.");
                GameEntity trolley =
                    _gameContext.GetEntityWithTrolleyPusherEntityId(worker.EntityId);
                if (trolley == null || trolley.isDestructed)
                {
                    RecoverMissingEmptyTrolley(worker, trolley);
                    continue;
                }
                ValidateEmptyTrolley(worker, trolley);
                GameEntity store = _gameContext.GetEntityWithEntityId(
                    worker.WarehouseWorkerStoreEntityId);
                if (store == null || store.isDestructed || !store.isStore)
                    throw new InvalidOperationException(
                        $"Returning worker {worker.EntityId} has invalid store.");
                Pose homePose = new(trolley.WorkerTrolleyHomePosition,
                    trolley.WorkerTrolleyHomeRotation);
                Vector3 pusherTarget = homePose.position -
                    homePose.rotation * Vector3.forward *
                    trolley.TrolleyFollowDistance;
                _navigation.SetManualRotation(
                    worker.NavigationAgent, homePose.rotation);
                if (store.isDayReportOpen)
                {
                    CompleteReturn(worker, trolley, homePose);
                    continue;
                }
                if (!HasReached(worker, pusherTarget))
                {
                    WorkerNavigationStateId state =
                        _navigation.GetState(worker.NavigationAgent);
                    if (state == WorkerNavigationStateId.Moving)
                        continue;
                    if (!_navigation.TrySetDestination(worker.NavigationAgent,
                            pusherTarget, _config.NavigationSampleRadius))
                    {
                        throw new InvalidOperationException(
                            $"Returning warehouse worker {worker.EntityId} cannot reach " +
                            $"the authored trolley home pusher pose {pusherTarget}.");
                    }
                    continue;
                }
                _navigation.Stop(worker.NavigationAgent);
                _navigation.SetManualRotation(
                    worker.NavigationAgent, homePose.rotation);
                if (!_navigation.HasReachedRotation(
                        worker.NavigationAgent, homePose.rotation, 2f))
                    continue;
                if (!CartReached(trolley, homePose))
                    continue;
                CompleteReturn(worker, trolley, homePose);
            }
        }

        private void CompleteReturn(GameEntity worker, GameEntity trolley,
            Pose homePose)
        {
            _navigation.Stop(worker.NavigationAgent);
            _hitch.Detach(trolley.Rigidbody);
            trolley.Rigidbody.position = homePose.position;
            trolley.Rigidbody.rotation = homePose.rotation;
            trolley.Transform.SetPositionAndRotation(
                homePose.position, homePose.rotation);
            trolley.RemoveTrolleyPusherEntityId();
            worker.isPushingWorkerTrolley = false;
            worker.isHandsOccupied = false;
            WorkerTrolleyLeaseUtility.ReleaseLease(trolley);
            _navigation.SetAutomaticRotation(
                worker.NavigationAgent, enabled: true);
            worker.ReplaceWarehouseWorkerStatus(worker.isWorkerShiftActive
                ? WarehouseWorkerStatusId.Idle
                : WarehouseWorkerStatusId.OffShift);
        }

        private void RecoverMissingEmptyTrolley(GameEntity worker,
            GameEntity destructedTrolley)
        {
            GameEntity storeTrolley =
                _gameContext.GetEntityWithWorkerTrolleyStoreEntityId(
                    worker.WarehouseWorkerStoreEntityId);
            if (storeTrolley != null && !storeTrolley.isDestructed)
                throw new InvalidOperationException(
                    $"Returning worker {worker.EntityId} lost a live trolley relation.");
            if (worker.isCarryingProduct ||
                _gameContext.GetEntityWithCarrierEntityId(worker.EntityId) != null)
                throw new InvalidOperationException(
                    $"Returning worker {worker.EntityId} lost a loaded trolley.");
            if (destructedTrolley != null &&
                destructedTrolley.hasTrolleyPusherEntityId)
            {
                if (destructedTrolley.TrolleyPusherEntityId != worker.EntityId)
                    throw new InvalidOperationException(
                        $"Destructed worker trolley has an unrelated pusher.");
                if (destructedTrolley.hasRigidbody)
                    _hitch.Detach(destructedTrolley.Rigidbody);
                destructedTrolley.RemoveTrolleyPusherEntityId();
            }
            _navigation.Stop(worker.NavigationAgent);
            worker.isPushingWorkerTrolley = false;
            worker.isHandsOccupied = false;
            _navigation.SetAutomaticRotation(
                worker.NavigationAgent, enabled: true);
            worker.ReplaceWarehouseWorkerStatus(worker.isWorkerShiftActive
                ? WarehouseWorkerStatusId.Idle
                : WarehouseWorkerStatusId.OffShift);
        }

        private void ValidateEmptyTrolley(GameEntity worker,
            GameEntity trolley)
        {
            if (trolley == null || trolley.isDestructed ||
                !trolley.isWorkerTrolley || !trolley.isPlatformTrolley ||
                trolley.isInteractable ||
                !trolley.hasEntityId || !trolley.hasTrolleyPusherEntityId ||
                trolley.TrolleyPusherEntityId != worker.EntityId ||
                !trolley.hasTrolleyStoreEntityId ||
                trolley.TrolleyStoreEntityId !=
                worker.WarehouseWorkerStoreEntityId ||
                !trolley.hasWorkerTrolleyStoreEntityId ||
                trolley.WorkerTrolleyStoreEntityId !=
                worker.WarehouseWorkerStoreEntityId ||
                !trolley.hasOccupiedTrolleySlotCount ||
                trolley.OccupiedTrolleySlotCount != 0 ||
                _gameContext.GetEntitiesWithWorkerTrolleyEntityId(
                    trolley.EntityId).Count != 0 ||
                _gameContext.GetEntitiesWithTrolleyEntityId(
                    trolley.EntityId).Count != 0 ||
                !trolley.hasTrolleyFollowDistance ||
                !trolley.hasWorkerTrolleyHomePosition ||
                !trolley.hasWorkerTrolleyHomeRotation ||
                !trolley.hasWorkerTrolleyCustomerLoadingPosition ||
                !trolley.hasWorkerTrolleyCustomerLoadingRotation ||
                !trolley.hasTransform || !trolley.hasRigidbody)
                throw new InvalidOperationException(
                    $"Returning worker {worker.EntityId} has invalid empty trolley.");
        }

        private bool HasReached(GameEntity worker, Vector3 destination)
        {
            float tolerance = Mathf.Max(_config.StoppingDistance, 0.05f);
            return _navigation.HasReachedDestination(worker.NavigationAgent,
                worker.Transform.position, destination, tolerance);
        }

        private bool CartReached(GameEntity trolley, Pose target)
        {
            float tolerance = Mathf.Max(_config.StoppingDistance, 0.1f);
            return (trolley.Transform.position - target.position).sqrMagnitude <=
                   tolerance * tolerance &&
                   Quaternion.Angle(trolley.Transform.rotation, target.rotation) <= 8f;
        }
    }
}
