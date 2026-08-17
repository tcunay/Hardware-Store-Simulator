using UnityEngine;
using UnityEngine.AI;

namespace HardwareStore.Gameplay.Common.Navigation
{
    public sealed class NavMeshWorkerNavigationService : IWorkerNavigationService
    {
        private readonly NavMeshPath _path = new NavMeshPath();

        public void Configure(NavMeshAgent agent, float speed, float acceleration,
            float angularSpeed, float stoppingDistance)
        {
            agent.speed = speed;
            agent.acceleration = acceleration;
            agent.angularSpeed = angularSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.autoBraking = true;
            agent.updatePosition = true;
            agent.updateRotation = true;
        }

        public bool TryEnsurePlacedOnNavMesh(NavMeshAgent agent, Vector3 position,
            float sampleRadius)
        {
            if (agent.isOnNavMesh)
                return true;
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit,
                    sampleRadius, NavMesh.AllAreas))
                return false;

            return agent.Warp(hit.position);
        }

        public bool TrySetDestination(NavMeshAgent agent, Vector3 destination,
            float sampleRadius)
        {
            if (!agent.isOnNavMesh ||
                !NavMesh.SamplePosition(destination, out NavMeshHit hit,
                    sampleRadius, agent.areaMask) ||
                !agent.CalculatePath(hit.position, _path) ||
                _path.status != NavMeshPathStatus.PathComplete)
                return false;

            return agent.SetPath(_path);
        }

        public void SetAutomaticRotation(NavMeshAgent agent, bool enabled) =>
            agent.updateRotation = enabled;

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

            return agent.hasPath ||
                   (currentPosition - destination).sqrMagnitude <=
                   fallbackTolerance * fallbackTolerance;
        }

        public void Stop(NavMeshAgent agent)
        {
            if (agent.isOnNavMesh && (agent.hasPath || agent.pathPending))
                agent.ResetPath();
        }
    }
}
