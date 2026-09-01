using UnityEngine;
using UnityEngine.AI;

namespace HardwareStore.Gameplay.Common.Physics
{
    public interface IWarehouseWorkerPhysicsMotor
    {
        void Step(Rigidbody body, Collider[] colliders, NavMeshAgent agent,
            float maximumSpeed, float acceleration, float angularSpeed);
    }
}
