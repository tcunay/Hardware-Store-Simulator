using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HardwareStore.Gameplay.Common.Navigation
{
    public sealed class NavMeshWorkerNavigationService : IWorkerNavigationService
    {
        private readonly NavMeshPath _path = new NavMeshPath();
        private readonly HashSet<NavMeshAgent> _automaticRotation = new();
        private readonly Dictionary<NavMeshAgent, Quaternion> _manualRotation = new();

        public void Configure(NavMeshAgent agent, float speed, float acceleration,
            float angularSpeed, float stoppingDistance)
        {
            agent.speed = speed;
            agent.acceleration = acceleration;
            agent.angularSpeed = angularSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType =
                ObstacleAvoidanceType.NoObstacleAvoidance;
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.isStopped = false;
            _manualRotation.Remove(agent);
            _automaticRotation.Add(agent);
        }

        public bool TryEnsurePlacedOnNavMesh(NavMeshAgent agent, Vector3 position,
            float sampleRadius)
        {
            if (agent.isOnNavMesh)
                return true;
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit,
                    sampleRadius, NavMesh.AllAreas))
                return false;

            bool placed = agent.Warp(hit.position);
            if (placed)
                agent.nextPosition = hit.position;
            return placed;
        }

        public bool TrySetDestination(NavMeshAgent agent, Vector3 destination,
            float sampleRadius)
        {
            if (!TryCalculateCompletePath(agent, destination, sampleRadius))
                return false;

            bool pathAssigned = agent.SetPath(_path);
            if (pathAssigned)
                agent.isStopped = false;
            return pathAssigned;
        }

        public bool CanReach(NavMeshAgent agent, Vector3 destination,
            float sampleRadius) =>
            TryCalculateCompletePath(agent, destination, sampleRadius);

        public void SetAutomaticRotation(NavMeshAgent agent, bool enabled)
        {
            agent.updateRotation = false;
            if (enabled)
            {
                _manualRotation.Remove(agent);
                _automaticRotation.Add(agent);
            }
            else
                _automaticRotation.Remove(agent);
        }

        public void SetManualRotation(NavMeshAgent agent, Quaternion target)
        {
            if (!IsFinite(target))
                throw new System.InvalidOperationException(
                    $"NavMesh agent '{agent.name}' received an invalid rotation target.");
            agent.updateRotation = false;
            _automaticRotation.Remove(agent);
            _manualRotation[agent] = Quaternion.Normalize(target);
        }

        public bool TryGetManualRotation(NavMeshAgent agent,
            out Quaternion target) =>
            _manualRotation.TryGetValue(agent, out target);

        public bool HasReachedRotation(NavMeshAgent agent, Quaternion target,
            float tolerance)
        {
            if (!IsFinite(target) || float.IsNaN(tolerance) ||
                float.IsInfinity(tolerance) || tolerance < 0f)
            {
                throw new System.InvalidOperationException(
                    "Worker rotation target and tolerance must be finite.");
            }

            Rigidbody body = agent.GetComponent<Rigidbody>();
            if (body == null)
                throw new System.InvalidOperationException(
                    $"NavMesh agent '{agent.name}' has no physical rotation owner.");
            return Quaternion.Angle(body.rotation, target) <= tolerance;
        }

        public WorkerNavigationStateId GetState(NavMeshAgent agent)
        {
            if (!agent.isOnNavMesh || agent.pathStatus == NavMeshPathStatus.PathInvalid)
                return WorkerNavigationStateId.PathUnavailable;
            if (agent.pathPending)
                return WorkerNavigationStateId.Moving;
            if (agent.hasPath && agent.remainingDistance > agent.stoppingDistance)
                return WorkerNavigationStateId.Moving;

            return WorkerNavigationStateId.Reached;
        }

        public bool HasReachedDestination(NavMeshAgent agent,
            Vector3 currentPosition, Vector3 destination,
            float fallbackTolerance)
        {
            if (GetState(agent) != WorkerNavigationStateId.Reached)
                return false;

            Vector3 offset = currentPosition - destination;
            offset.y = 0f;
            return offset.sqrMagnitude <=
                   fallbackTolerance * fallbackTolerance;
        }

        public WorkerNavigationIntent GetPlannedIntent(NavMeshAgent agent)
        {
            if (!agent.isOnNavMesh || agent.pathStatus == NavMeshPathStatus.PathInvalid ||
                (!agent.hasPath && !agent.pathPending))
            {
                return new WorkerNavigationIntent(Vector3.zero, 0f);
            }

            Vector3 offset = agent.steeringTarget - agent.nextPosition;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance <= 0.0001f)
                return new WorkerNavigationIntent(Vector3.zero, 0f);

            return new WorkerNavigationIntent(
                offset / distance * agent.speed,
                distance);
        }

        public WorkerNavigationIntent GetIntent(NavMeshAgent agent) =>
            agent.isStopped
                ? new WorkerNavigationIntent(Vector3.zero, 0f)
                : GetPlannedIntent(agent);

        public bool UsesAutomaticRotation(NavMeshAgent agent) =>
            _automaticRotation.Contains(agent);

        public void SetPaused(NavMeshAgent agent, bool paused)
        {
            if (!agent.isOnNavMesh)
                throw new System.InvalidOperationException(
                    $"NavMesh agent '{agent.name}' cannot change traffic pause off NavMesh.");

            agent.isStopped = paused;
        }

        public void Stop(NavMeshAgent agent)
        {
            if (agent.isOnNavMesh && agent.isStopped)
                agent.isStopped = false;
            if (agent.isOnNavMesh && (agent.hasPath || agent.pathPending))
                agent.ResetPath();
        }

        private bool TryCalculateCompletePath(NavMeshAgent agent,
            Vector3 destination, float sampleRadius) =>
            agent.isOnNavMesh &&
            NavMesh.SamplePosition(destination, out NavMeshHit hit,
                sampleRadius, agent.areaMask) &&
            agent.CalculatePath(hit.position, _path) &&
            _path.status == NavMeshPathStatus.PathComplete;

        private static bool IsFinite(Quaternion value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z) &&
            !float.IsNaN(value.w) && !float.IsInfinity(value.w);
    }
}
