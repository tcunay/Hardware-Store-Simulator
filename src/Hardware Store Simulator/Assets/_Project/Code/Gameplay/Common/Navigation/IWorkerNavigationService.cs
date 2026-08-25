using UnityEngine;
using UnityEngine.AI;

namespace HardwareStore.Gameplay.Common.Navigation
{
    public enum WorkerNavigationStateId
    {
        Moving,
        Reached,
        PathUnavailable,
    }

    public interface IWorkerNavigationService
    {
        void Configure(NavMeshAgent agent, float speed, float acceleration,
            float angularSpeed, float stoppingDistance);
        bool TryEnsurePlacedOnNavMesh(NavMeshAgent agent, Vector3 position,
            float sampleRadius);
        bool CanReach(NavMeshAgent agent, Vector3 destination,
            float sampleRadius);
        bool TrySetDestination(NavMeshAgent agent, Vector3 destination,
            float sampleRadius);
        void SetAutomaticRotation(NavMeshAgent agent, bool enabled);
        WorkerNavigationStateId GetState(NavMeshAgent agent);
        bool HasReachedDestination(NavMeshAgent agent, Vector3 currentPosition,
            Vector3 destination, float fallbackTolerance);
        void Stop(NavMeshAgent agent);
    }
}
