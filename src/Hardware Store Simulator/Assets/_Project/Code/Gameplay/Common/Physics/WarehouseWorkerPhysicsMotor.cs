using System;
using System.Collections.Generic;
using HardwareStore.Gameplay.Common.Navigation;
using HardwareStore.Gameplay.Common.Time;
using UnityEngine;
using UnityEngine.AI;

namespace HardwareStore.Gameplay.Common.Physics
{
    public sealed class WarehouseWorkerPhysicsMotor :
        IWarehouseWorkerPhysicsMotor
    {
        private const float MinimumMotion = 0.0001f;
        private const float CollisionSkin = 0.025f;
        private const float BrakingMultiplier = 1.5f;
        private const int SolverIterations = 12;
        private const int SolverVelocityIterations = 4;

        private readonly IPhysicsTimeService _time;
        private readonly IWorkerNavigationService _navigation;
        private readonly Dictionary<Rigidbody, Vector3> _linearVelocities = new();

        public WarehouseWorkerPhysicsMotor(IPhysicsTimeService time,
            IWorkerNavigationService navigation)
        {
            _time = time;
            _navigation = navigation;
        }

        public void Step(Rigidbody body, Collider[] colliders,
            NavMeshAgent agent, Rigidbody coupledBody,
            Collider[] coupledColliders, float maximumSpeed,
            float acceleration, float angularSpeed, bool yielding)
        {
            if (body != null)
            {
                body.solverIterations = SolverIterations;
                body.solverVelocityIterations = SolverVelocityIterations;
            }
            Validate(body, colliders, agent, coupledBody, coupledColliders,
                maximumSpeed, acceleration, angularSpeed);
            float deltaTime = _time.FixedDeltaTime;
            if (!IsFinite(deltaTime) || deltaTime <= 0f)
                throw new InvalidOperationException(
                    "Warehouse-worker fixed delta time must be finite and positive.");

            agent.nextPosition = body.position;
            Vector3 currentVelocity = _linearVelocities.TryGetValue(
                body, out Vector3 storedVelocity)
                ? storedVelocity
                : Vector3.zero;
            Vector3 desiredVelocity = yielding || agent.isStopped ||
                                      !agent.hasPath || agent.pathPending
                ? Vector3.zero
                : Vector3.ClampMagnitude(
                    Horizontal(agent.desiredVelocity), maximumSpeed);
            float rate = desiredVelocity.sqrMagnitude <
                         currentVelocity.sqrMagnitude
                ? acceleration * BrakingMultiplier
                : acceleration;
            Vector3 resolvedVelocity = Vector3.MoveTowards(
                currentVelocity, desiredVelocity, rate * deltaTime);
            Vector3 displacement = resolvedVelocity * deltaTime;
            if (displacement.sqrMagnitude > MinimumMotion * MinimumMotion)
            {
                Vector3 direction = displacement.normalized;
                float allowedDistance = ResolveAllowedDistance(
                    body, direction, displacement.magnitude);
                if (coupledBody != null)
                {
                    allowedDistance = Mathf.Min(
                        allowedDistance,
                        ResolveAllowedDistance(
                            coupledBody, direction, displacement.magnitude));
                }

                displacement = direction * allowedDistance;
                resolvedVelocity = displacement / deltaTime;
            }

            _linearVelocities[body] = resolvedVelocity;
            body.MovePosition(body.position + displacement);
            Quaternion targetRotation;
            if (_navigation.TryGetManualRotation(agent, out Quaternion manualTarget))
                targetRotation = manualTarget;
            else if (_navigation.UsesAutomaticRotation(agent) &&
                     resolvedVelocity.sqrMagnitude > MinimumMotion * MinimumMotion)
                targetRotation = Quaternion.LookRotation(
                    resolvedVelocity.normalized, Vector3.up);
            else
                return;

            body.MoveRotation(Quaternion.RotateTowards(
                body.rotation,
                targetRotation,
                angularSpeed * deltaTime));
        }

        private static float ResolveAllowedDistance(Rigidbody body,
            Vector3 direction, float requestedDistance)
        {
            if (!body.SweepTest(
                    direction,
                    out RaycastHit hit,
                    requestedDistance + CollisionSkin,
                    QueryTriggerInteraction.Ignore))
            {
                return requestedDistance;
            }

            return Mathf.Min(
                requestedDistance,
                Mathf.Max(hit.distance - CollisionSkin, 0f));
        }

        private static void Validate(Rigidbody body, Collider[] colliders,
            NavMeshAgent agent, Rigidbody coupledBody,
            Collider[] coupledColliders, float maximumSpeed,
            float acceleration, float angularSpeed)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));
            if (colliders == null || colliders.Length == 0)
                throw new InvalidOperationException(
                    "Warehouse worker requires registered colliders.");
            if (agent == null)
                throw new ArgumentNullException(nameof(agent));
            if (!body.gameObject.activeInHierarchy || !body.isKinematic ||
                body.useGravity || !body.detectCollisions)
            {
                throw new InvalidOperationException(
                    "Warehouse-worker motor requires an active collision-enabled " +
                    "kinematic Rigidbody without gravity.");
            }
            if (body.interpolation != RigidbodyInterpolation.Interpolate)
                throw new InvalidOperationException(
                    "Warehouse-worker Rigidbody must use interpolation.");
            if (agent.gameObject != body.gameObject ||
                agent.updatePosition || agent.updateRotation)
            {
                throw new InvalidOperationException(
                    "Warehouse-worker NavMeshAgent must share the Rigidbody root and " +
                    "delegate pose ownership to the physics motor.");
            }
            foreach (Collider collider in colliders)
            {
                if (collider == null || collider.attachedRigidbody != body ||
                    !collider.enabled || collider.isTrigger)
                {
                    throw new InvalidOperationException(
                        "Warehouse-worker colliders must be enabled solid shapes attached " +
                        "to its Rigidbody.");
                }
            }
            bool hasCoupledBody = coupledBody != null;
            bool hasCoupledColliders = coupledColliders != null;
            if (hasCoupledBody != hasCoupledColliders)
            {
                throw new InvalidOperationException(
                    "Warehouse-worker coupled physics requires both body and colliders.");
            }
            if (hasCoupledBody)
            {
                if (!coupledBody.gameObject.activeInHierarchy ||
                    coupledBody.isKinematic || !coupledBody.useGravity ||
                    !coupledBody.detectCollisions || coupledColliders.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Warehouse-worker coupled body must be active, dynamic and " +
                        "collision-enabled.");
                }
                bool hasSolidCoupledCollider = false;
                foreach (Collider collider in coupledColliders)
                {
                    if (collider == null ||
                        collider.attachedRigidbody != coupledBody)
                    {
                        throw new InvalidOperationException(
                            "Warehouse-worker coupled colliders must belong to the " +
                            "coupled Rigidbody.");
                    }

                    hasSolidCoupledCollider |=
                        collider.enabled &&
                        collider.gameObject.activeInHierarchy &&
                        !collider.isTrigger;
                }

                if (!hasSolidCoupledCollider)
                {
                    throw new InvalidOperationException(
                        "Warehouse-worker coupled body requires at least one enabled " +
                        "solid collider.");
                }
            }
            if (!IsFinite(maximumSpeed) || maximumSpeed <= 0f ||
                !IsFinite(acceleration) || acceleration <= 0f ||
                !IsFinite(angularSpeed) || angularSpeed <= 0f)
            {
                throw new InvalidOperationException(
                    "Warehouse-worker motor values must be finite and positive.");
            }
        }

        private static Vector3 Horizontal(Vector3 value) =>
            new(value.x, 0f, value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
