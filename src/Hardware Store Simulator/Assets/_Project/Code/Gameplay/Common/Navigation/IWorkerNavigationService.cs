using UnityEngine;
using UnityEngine.AI;

namespace HardwareStore.Gameplay.Common.Navigation
{
    public readonly struct WorkerNavigationIntent
    {
        public WorkerNavigationIntent(Vector3 velocity, float distance)
        {
            Velocity = velocity;
            Distance = distance;
        }

        public Vector3 Velocity { get; }
        public float Distance { get; }
    }

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
        void SetManualRotation(NavMeshAgent agent, Quaternion target);
        bool TryGetManualRotation(NavMeshAgent agent, out Quaternion target);
        bool HasReachedRotation(NavMeshAgent agent, Quaternion target,
            float tolerance);
        WorkerNavigationStateId GetState(NavMeshAgent agent);
        bool HasReachedDestination(NavMeshAgent agent, Vector3 currentPosition,
            Vector3 destination, float fallbackTolerance);
        WorkerNavigationIntent GetPlannedIntent(NavMeshAgent agent);
        WorkerNavigationIntent GetIntent(NavMeshAgent agent);
        bool UsesAutomaticRotation(NavMeshAgent agent);
        void SetPaused(NavMeshAgent agent, bool paused);
        void Stop(NavMeshAgent agent);
    }
}
