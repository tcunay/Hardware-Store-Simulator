using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Navigation;
using HardwareStore.Gameplay.Common.Physics;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Features.Employees;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Trolley.Systems
{
    public sealed class FollowWorkerTrolleySystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly ITrolleyMotionService _motion;
        private readonly IWorkerNavigationService _navigation;
        private readonly IGroup<GameEntity> _trolleys;
        private readonly List<GameEntity> _buffer = new(2);

        public FollowWorkerTrolleySystem(GameContext gameContext,
            ITrolleyMotionService motion, IWorkerNavigationService navigation)
        {
            _gameContext = gameContext;
            _motion = motion;
            _navigation = navigation;
            _trolleys = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WorkerTrolley, GameMatcher.EntityId,
                    GameMatcher.TrolleyPusherEntityId,
                    GameMatcher.TrolleyFollowDistance,
                    GameMatcher.WorkerTrolleyHomePosition,
                    GameMatcher.WorkerTrolleyHomeRotation,
                    GameMatcher.Transform, GameMatcher.Rigidbody,
                    GameMatcher.Colliders)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity trolley in _trolleys.GetEntities(_buffer))
            {
                GameEntity worker = _gameContext.GetEntityWithEntityId(
                    trolley.TrolleyPusherEntityId);
                if (worker == null || worker.isDestructed ||
                    !worker.isWarehouseWorker || !worker.hasEntityId ||
                    !worker.hasTransform || !worker.hasNavigationAgent ||
                    !worker.hasWarehouseWorkerStatus ||
                    !worker.isHandsOccupied || !worker.isPushingWorkerTrolley ||
                    worker.isCarryingProduct || !trolley.Rigidbody.isKinematic)
                    throw new InvalidOperationException(
                        $"Worker trolley {trolley.EntityId} has invalid pusher.");

                Transform workerTransform = worker.Transform;
                Vector3 position = workerTransform.position +
                    workerTransform.forward * trolley.TrolleyFollowDistance;
                Quaternion rotation = workerTransform.rotation;
                if (!_motion.TryResolveMove(
                        trolley.Rigidbody, trolley.Colliders,
                        workerTransform, 0f, position, rotation,
                        out Pose resolvedPose))
                {
                    HandleObstruction(worker, trolley);
                    continue;
                }

                trolley.Rigidbody.position = resolvedPose.position;
                trolley.Rigidbody.rotation = resolvedPose.rotation;
                trolley.Transform.SetPositionAndRotation(
                    resolvedPose.position, resolvedPose.rotation);
            }
        }

        private void HandleObstruction(GameEntity worker, GameEntity trolley)
        {
            _navigation.Stop(worker.NavigationAgent);
            GameEntity run =
                _gameContext.GetEntityWithWarehouseTaskWorkerTrolleyEntityId(
                    trolley.EntityId);
            if (run != null && !run.isDestructed)
            {
                bool outboundRun = run.isWorkerTrolleyCustomerLoadingRun &&
                    !run.isWorkerTrolleyInboundStorageRun &&
                    !run.isInboundToStorageTask;
                bool inboundRun = run.isWorkerTrolleyInboundStorageRun &&
                    !run.isWorkerTrolleyCustomerLoadingRun &&
                    run.isInboundToStorageTask;
                bool validRunRole = outboundRun ^ inboundRun;
                if (!run.isWarehouseTask || !validRunRole ||
                    !run.hasAssignedWorkerEntityId ||
                    run.AssignedWorkerEntityId != worker.EntityId ||
                    !run.hasWarehouseTaskStep ||
                    run.WarehouseTaskStep == WarehouseTaskStepId.Blocked ||
                    !run.hasWarehouseTaskBlockReason)
                    throw new InvalidOperationException(
                        $"Worker trolley {trolley.EntityId} has invalid active run.");
                run.ReplaceWarehouseTaskStep(WarehouseTaskStepId.Blocked);
                run.ReplaceWarehouseTaskBlockReason(
                    WarehouseTaskBlockReasonId.WorkerTrolleyObstructed);
                return;
            }

            if (worker.WarehouseWorkerStatus !=
                WarehouseWorkerStatusId.ReturningWorkerTrolley ||
                trolley.OccupiedTrolleySlotCount != 0 ||
                _gameContext.GetEntitiesWithWorkerTrolleyEntityId(
                    trolley.EntityId).Count != 0)
                throw new InvalidOperationException(
                    $"Worker trolley {trolley.EntityId} is obstructed without a run.");
            NormalizeEmptyReturn(worker, trolley);
        }

        private void NormalizeEmptyReturn(GameEntity worker,
            GameEntity trolley)
        {
            trolley.Rigidbody.position = trolley.WorkerTrolleyHomePosition;
            trolley.Rigidbody.rotation = trolley.WorkerTrolleyHomeRotation;
            trolley.Transform.SetPositionAndRotation(
                trolley.WorkerTrolleyHomePosition,
                trolley.WorkerTrolleyHomeRotation);
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
    }
}
