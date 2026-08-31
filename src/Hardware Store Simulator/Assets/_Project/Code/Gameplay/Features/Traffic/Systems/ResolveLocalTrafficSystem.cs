using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Navigation;
using HardwareStore.Gameplay.Common.Traffic;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Traffic.Systems
{
    public sealed class ResolveLocalTrafficSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly ILocalTrafficPredictionService _prediction;
        private readonly IWorkerNavigationService _navigation;
        private readonly LocalTrafficConfig _config;
        private readonly IGroup<GameEntity> _participants;
        private readonly List<GameEntity> _buffer = new(16);
        private readonly List<GameEntity> _yieldPath = new(8);
        private readonly Dictionary<int, int> _yieldPathIndices = new(8);

        public ResolveLocalTrafficSystem(GameContext gameContext,
            ILocalTrafficPredictionService prediction,
            IWorkerNavigationService navigation,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _prediction = prediction;
            _navigation = navigation;
            _config = staticData.LocalTraffic;
            _participants = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.TrafficParticipant,
                    GameMatcher.EntityId,
                    GameMatcher.TrafficControlPolicy,
                    GameMatcher.TrafficPriority,
                    GameMatcher.TrafficDesiredVelocity,
                    GameMatcher.TrafficIntentDistance,
                    GameMatcher.TrafficAngularIntent,
                    GameMatcher.Transform)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            IReadOnlyList<GameEntity> participants =
                _participants.GetEntities(_buffer);
            foreach (GameEntity mover in participants)
            {
                if (!CanYield(mover))
                    continue;
                if (mover.TrafficDesiredVelocity.sqrMagnitude <=
                        _config.StationarySpeed * _config.StationarySpeed &&
                    Mathf.Abs(mover.TrafficAngularIntent) <= 0f)
                {
                    ClearYield(mover);
                    continue;
                }

                if (TryKeepExistingYield(mover, participants))
                    continue;
                bool hasParticipantConflict = TryFindParticipantConflict(
                    mover,
                    participants,
                    out GameEntity conflictOwner,
                    out float participantConflictTime);
                bool hasWorldConflict = TryFindWorldConflict(
                    mover,
                    useReleaseClearance: false,
                    out LocalTrafficWorldPrediction worldConflict);
                if (hasWorldConflict && (!hasParticipantConflict ||
                    worldConflict.FirstConflictTime < participantConflictTime))
                {
                    SetWorldYield(mover, worldConflict.Collider);
                }
                else if (hasParticipantConflict)
                {
                    SetYield(mover, conflictOwner);
                }
                else
                {
                    ClearYield(mover);
                }
            }

            BreakYieldCycles(participants);
        }

        private void BreakYieldCycles(IReadOnlyList<GameEntity> participants)
        {
            foreach (GameEntity start in participants)
            {
                if (!start.isTrafficYielding ||
                    !start.hasTrafficConflictEntityId)
                {
                    continue;
                }

                _yieldPath.Clear();
                _yieldPathIndices.Clear();
                GameEntity current = start;
                while (current != null && !current.isDestructed &&
                       current.isTrafficYielding &&
                       current.hasTrafficConflictEntityId)
                {
                    if (_yieldPathIndices.TryGetValue(
                            current.EntityId, out int cycleStart))
                    {
                        GameEntity winner = _yieldPath[cycleStart];
                        for (int index = cycleStart + 1;
                             index < _yieldPath.Count;
                             index++)
                        {
                            GameEntity candidate = _yieldPath[index];
                            if (candidate.TrafficPriority > winner.TrafficPriority ||
                                candidate.TrafficPriority == winner.TrafficPriority &&
                                candidate.EntityId < winner.EntityId)
                            {
                                winner = candidate;
                            }
                        }

                        ClearYield(winner);
                        break;
                    }

                    _yieldPathIndices.Add(current.EntityId, _yieldPath.Count);
                    _yieldPath.Add(current);
                    current = _gameContext.GetEntityWithEntityId(
                        current.TrafficConflictEntityId);
                }
            }
        }

        private bool TryKeepExistingYield(GameEntity mover,
            IReadOnlyList<GameEntity> participants)
        {
            if (!mover.isTrafficYielding)
                return false;
            if (mover.hasTrafficConflictEntityId ==
                mover.hasTrafficConflictCollider)
            {
                throw new InvalidOperationException(
                    $"Traffic participant {mover.EntityId} has an ambiguous yield relation.");
            }
            if (mover.hasTrafficConflictCollider)
            {
                if (mover.TrafficConflictCollider != null &&
                    TryFindWorldConflict(
                        mover,
                        useReleaseClearance: true,
                        out LocalTrafficWorldPrediction worldConflict))
                {
                    SetWorldYield(mover, worldConflict.Collider);
                    return true;
                }

                ClearYield(mover);
                return false;
            }

            int conflictOwnerId = mover.TrafficConflictEntityId;
            GameEntity conflictOwner = _gameContext.GetEntityWithEntityId(
                conflictOwnerId);
            if (conflictOwner == null || conflictOwner.isDestructed ||
                !conflictOwner.isTrafficParticipant)
            {
                ClearYield(mover);
                return false;
            }
            if (HasConflictWithOwner(
                    mover,
                    mover,
                    participants,
                    conflictOwnerId,
                    useReleaseClearance: true))
            {
                ApplyNavigationPause(mover, paused: true);
                return true;
            }
            foreach (GameEntity participant in participants)
            {
                if (ReferenceEquals(participant, mover) ||
                    participant.TrafficControlPolicy !=
                    TrafficControlPolicyId.Coupled ||
                    !ReferenceEquals(GetMotionOwner(participant), mover))
                {
                    continue;
                }
                if (HasConflictWithOwner(
                        mover,
                        participant,
                        participants,
                        conflictOwnerId,
                        useReleaseClearance: true))
                {
                    ApplyNavigationPause(mover, paused: true);
                    return true;
                }
            }

            ClearYield(mover);
            return false;
        }

        private bool HasConflictWithOwner(GameEntity mover,
            GameEntity moverFootprint,
            IReadOnlyList<GameEntity> participants,
            int conflictOwnerId,
            bool useReleaseClearance)
        {
            foreach (GameEntity obstacle in participants)
            {
                if (ShouldIgnore(mover, obstacle) ||
                    GetMotionOwner(obstacle).EntityId != conflictOwnerId)
                {
                    continue;
                }
                ResolveObstacleMotion(obstacle,
                    out Vector3 obstacleVelocity,
                    out float obstacleDistance);
                if (_prediction.TryPredictConflict(
                        moverFootprint,
                        mover,
                        moverFootprint.TrafficDesiredVelocity,
                        moverFootprint.TrafficIntentDistance,
                        obstacle,
                        GetMotionOwner(obstacle),
                        obstacleVelocity,
                        obstacleDistance,
                        useReleaseClearance,
                        out _))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryFindParticipantConflict(GameEntity mover,
            IReadOnlyList<GameEntity> participants,
            out GameEntity conflictOwner, out float earliestConflict)
        {
            conflictOwner = null;
            earliestConflict = float.PositiveInfinity;
            FindParticipantConflictForFootprint(
                mover,
                mover,
                participants,
                ref conflictOwner,
                ref earliestConflict);
            foreach (GameEntity participant in participants)
            {
                if (ReferenceEquals(participant, mover) ||
                    participant.TrafficControlPolicy !=
                    TrafficControlPolicyId.Coupled ||
                    !ReferenceEquals(GetMotionOwner(participant), mover))
                {
                    continue;
                }

                FindParticipantConflictForFootprint(
                    mover,
                    participant,
                    participants,
                    ref conflictOwner,
                    ref earliestConflict);
            }

            return conflictOwner != null;
        }

        private void FindParticipantConflictForFootprint(
            GameEntity mover,
            GameEntity moverFootprint,
            IReadOnlyList<GameEntity> participants,
            ref GameEntity conflictOwner,
            ref float earliestConflict)
        {
            foreach (GameEntity obstacle in participants)
            {
                if (ShouldIgnore(mover, obstacle))
                    continue;
                ResolveObstacleMotion(obstacle,
                    out Vector3 obstacleVelocity,
                    out float obstacleDistance);
                if (!_prediction.TryPredictConflict(
                        moverFootprint,
                        mover,
                        moverFootprint.TrafficDesiredVelocity,
                        moverFootprint.TrafficIntentDistance,
                        obstacle,
                        GetMotionOwner(obstacle),
                        obstacleVelocity,
                        obstacleDistance,
                        useReleaseClearance: false,
                        out LocalTrafficPrediction result))
                {
                    continue;
                }

                GameEntity owner = GetMotionOwner(obstacle);
                if (!MoverMustYield(
                        mover, owner, obstacleVelocity, result))
                    continue;
                if (result.FirstConflictTime > earliestConflict ||
                    (Mathf.Approximately(result.FirstConflictTime,
                         earliestConflict) && conflictOwner != null &&
                     owner.EntityId >= conflictOwner.EntityId))
                {
                    continue;
                }

                earliestConflict = result.FirstConflictTime;
                conflictOwner = owner;
            }
        }

        private bool TryFindWorldConflict(GameEntity mover,
            bool useReleaseClearance,
            out LocalTrafficWorldPrediction earliest)
        {
            GameEntity coupled =
                _gameContext.GetEntityWithTrolleyPusherEntityId(mover.EntityId);
            if (coupled != null && (coupled.isDestructed ||
                !coupled.isTrafficParticipant ||
                coupled.TrafficControlPolicy != TrafficControlPolicyId.Coupled ||
                !coupled.hasTransform || !coupled.hasTrafficDesiredVelocity ||
                !coupled.hasTrafficIntentDistance))
            {
                throw new InvalidOperationException(
                    $"Traffic participant {mover.EntityId} has an invalid coupled footprint.");
            }

            bool found = _prediction.TryPredictWorldConflict(
                mover,
                mover,
                mover.TrafficDesiredVelocity,
                mover.TrafficIntentDistance,
                coupled?.Transform,
                useReleaseClearance,
                out earliest);
            if (coupled == null || !_prediction.TryPredictWorldConflict(
                    coupled,
                    mover,
                    coupled.TrafficDesiredVelocity,
                    coupled.TrafficIntentDistance,
                    mover.Transform,
                    useReleaseClearance,
                    out LocalTrafficWorldPrediction candidate))
            {
                return found;
            }
            if (!found || candidate.FirstConflictTime < earliest.FirstConflictTime)
                earliest = candidate;

            return true;
        }

        private bool MoverMustYield(GameEntity mover, GameEntity obstacleOwner,
            Vector3 obstacleVelocity, LocalTrafficPrediction prediction)
        {
            if (obstacleOwner.TrafficControlPolicy ==
                TrafficControlPolicyId.Uncontrolled)
            {
                return true;
            }
            if (obstacleOwner.isTrafficYielding)
                return true;
            if (obstacleVelocity.sqrMagnitude <=
                    _config.StationarySpeed * _config.StationarySpeed &&
                Mathf.Abs(obstacleOwner.TrafficAngularIntent) <= 0f)
            {
                return true;
            }

            float tolerance = _config.ArrivalPriorityTolerance;
            if (prediction.MoverArrivalTime + tolerance <
                prediction.ObstacleArrivalTime)
            {
                return false;
            }
            if (prediction.ObstacleArrivalTime + tolerance <
                prediction.MoverArrivalTime)
            {
                return true;
            }
            if (mover.TrafficPriority != obstacleOwner.TrafficPriority)
                return mover.TrafficPriority < obstacleOwner.TrafficPriority;

            return mover.EntityId > obstacleOwner.EntityId;
        }

        private void ResolveObstacleMotion(GameEntity obstacle,
            out Vector3 velocity, out float distance)
        {
            GameEntity owner = GetMotionOwner(obstacle);
            if (owner.isRouteMover)
            {
                if (!owner.hasTrafficCurrentSpeed || !owner.hasMovementSpeed)
                {
                    throw new InvalidOperationException(
                        $"Route traffic participant {owner.EntityId} has no speed state.");
                }

                Vector3 direction = Horizontal(
                    obstacle.TrafficDesiredVelocity);
                if (direction.sqrMagnitude <=
                    _config.StationarySpeed * _config.StationarySpeed)
                {
                    velocity = Vector3.zero;
                    distance = 0f;
                    return;
                }

                if (owner.isTrafficYielding)
                {
                    velocity = direction.normalized *
                               owner.TrafficCurrentSpeed;
                    distance = Mathf.Min(
                        obstacle.TrafficIntentDistance,
                        ResolveRouteStoppingDistance(
                            owner.TrafficCurrentSpeed,
                            _config.VehicleBraking));
                    return;
                }

                velocity = obstacle.TrafficDesiredVelocity;
                distance = obstacle.TrafficIntentDistance;
                return;
            }
            if (owner.hasNavigationAgent)
            {
                WorkerNavigationIntent intent =
                    _navigation.GetIntent(owner.NavigationAgent);
                velocity = Horizontal(intent.Velocity);
                distance = Mathf.Min(
                    obstacle.TrafficIntentDistance,
                    intent.Distance);
                return;
            }

            velocity = obstacle.TrafficDesiredVelocity;
            distance = obstacle.TrafficIntentDistance;
        }

        private static float ResolveRouteStoppingDistance(float currentSpeed,
            float braking)
        {
            if (currentSpeed < 0f || braking <= 0f)
                throw new InvalidOperationException(
                    "Traffic route prediction requires non-negative speed and braking.");

            return currentSpeed * currentSpeed / (2f * braking);
        }

        private bool ShouldIgnore(GameEntity mover, GameEntity obstacle)
        {
            if (ReferenceEquals(mover, obstacle) ||
                obstacle.isPlayer && obstacle.isDrivingForklift ||
                IsCustomerActorIgnoringOwnVehicle(mover, obstacle) ||
                IsWorkerServicingAssignedCustomerVehicle(mover, obstacle) ||
                IsWorkerApproachingAssignedTrolley(mover, obstacle))
            {
                return true;
            }

            GameEntity owner = GetMotionOwner(obstacle);
            return ReferenceEquals(mover, owner) ||
                   mover.hasTrolleyPusherEntityId &&
                   mover.TrolleyPusherEntityId == owner.EntityId;
        }

        private static bool IsCustomerActorIgnoringOwnVehicle(
            GameEntity mover, GameEntity obstacle) =>
            mover.isCustomer && mover.hasCustomerActorVisitEntityId &&
            obstacle.isCustomerVisit && obstacle.isCustomerVehicle &&
            obstacle.hasEntityId &&
            obstacle.EntityId == mover.CustomerActorVisitEntityId;

        private bool IsWorkerServicingAssignedCustomerVehicle(
            GameEntity mover, GameEntity obstacle)
        {
            if (!mover.isWarehouseWorker || !obstacle.isCustomerVisit ||
                !obstacle.isCustomerVehicle)
            {
                return false;
            }

            GameEntity task = _gameContext.GetEntityWithAssignedWorkerEntityId(
                mover.EntityId);
            return task != null && !task.isDestructed && task.isWarehouseTask &&
                   task.hasWarehouseTaskCustomerVisitEntityId &&
                   task.WarehouseTaskCustomerVisitEntityId == obstacle.EntityId;
        }

        private bool IsWorkerApproachingAssignedTrolley(
            GameEntity mover, GameEntity obstacle)
        {
            if (!mover.isWarehouseWorker || !obstacle.isWorkerTrolley)
                return false;

            GameEntity task = _gameContext.GetEntityWithAssignedWorkerEntityId(
                mover.EntityId);
            return task != null && !task.isDestructed && task.isWarehouseTask &&
                   task.hasWarehouseTaskStep &&
                   task.WarehouseTaskStep ==
                   WarehouseTaskStepId.MovingToWorkerTrolley &&
                   task.hasWarehouseTaskWorkerTrolleyEntityId &&
                   task.WarehouseTaskWorkerTrolleyEntityId == obstacle.EntityId;
        }

        private GameEntity GetMotionOwner(GameEntity participant)
        {
            if (!participant.hasTrolleyPusherEntityId)
                return participant;

            GameEntity owner = _gameContext.GetEntityWithEntityId(
                participant.TrolleyPusherEntityId);
            if (owner == null || owner.isDestructed ||
                !owner.isTrafficParticipant || !owner.hasTrafficControlPolicy ||
                !owner.hasTrafficPriority || !owner.hasTrafficDesiredVelocity)
            {
                throw new InvalidOperationException(
                    $"Traffic participant {participant.EntityId} has an invalid motion owner.");
            }
            return owner;
        }

        private static Vector3 Horizontal(Vector3 value) =>
            new(value.x, 0f, value.z);

        private static bool CanYield(GameEntity participant) =>
            participant.TrafficControlPolicy is
                TrafficControlPolicyId.BrakeOnly or
                TrafficControlPolicyId.NavMesh;

        private void SetYield(GameEntity mover, GameEntity conflictOwner)
        {
            mover.isTrafficYielding = true;
            if (mover.hasTrafficConflictCollider)
                mover.RemoveTrafficConflictCollider();
            if (mover.hasTrafficConflictEntityId)
                mover.ReplaceTrafficConflictEntityId(conflictOwner.EntityId);
            else
                mover.AddTrafficConflictEntityId(conflictOwner.EntityId);
            ApplyNavigationPause(mover, paused: true);
        }

        private void SetWorldYield(GameEntity mover, Collider collider)
        {
            if (collider == null)
                throw new ArgumentNullException(nameof(collider));

            mover.isTrafficYielding = true;
            if (mover.hasTrafficConflictEntityId)
                mover.RemoveTrafficConflictEntityId();
            if (mover.hasTrafficConflictCollider)
                mover.ReplaceTrafficConflictCollider(collider);
            else
                mover.AddTrafficConflictCollider(collider);
            ApplyNavigationPause(mover, paused: true);
        }

        private void ClearYield(GameEntity mover)
        {
            bool wasYielding = mover.isTrafficYielding;
            if (mover.hasTrafficConflictEntityId)
                mover.RemoveTrafficConflictEntityId();
            if (mover.hasTrafficConflictCollider)
                mover.RemoveTrafficConflictCollider();
            mover.isTrafficYielding = false;
            if (wasYielding)
                ApplyNavigationPause(mover, paused: false);
        }

        private void ApplyNavigationPause(GameEntity mover, bool paused)
        {
            if (mover.TrafficControlPolicy != TrafficControlPolicyId.NavMesh)
                return;
            if (!mover.hasNavigationAgent)
            {
                throw new InvalidOperationException(
                    $"NavMesh traffic participant {mover.EntityId} has no agent.");
            }

            _navigation.SetPaused(mover.NavigationAgent, paused);
        }
    }
}
