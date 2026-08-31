using UnityEngine;
using UnityEngine.AI;

namespace HardwareStore.Gameplay.Common.Physics
{
    public interface IWarehouseWorkerPhysicsMotor
    {
        void Step(Rigidbody body, Collider[] colliders, NavMeshAgent agent,
            Rigidbody coupledBody, Collider[] coupledColliders,
            float maximumSpeed, float acceleration, float angularSpeed,
            bool yielding);
    }
}
