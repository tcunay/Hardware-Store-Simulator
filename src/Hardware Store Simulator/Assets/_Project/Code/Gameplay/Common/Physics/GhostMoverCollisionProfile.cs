using System;
using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public static class GhostMoverCollisionProfile
    {
        public const string GhostMover = "GhostMover";
        public const string TrafficObstacle = "TrafficObstacle";

        private const int PhysicsLayerCount = 32;
        private static bool _runtimeMatrixValidated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeValidation() =>
            _runtimeMatrixValidated = false;

        public static int WithoutGhostMover(int layerMask) =>
            layerMask & ~(1 << RequireLayer(GhostMover));

        public static void Apply(Rigidbody body, Collider[] colliders,
            string layerName)
        {
            int layer = RequireProfileLayer(layerName);
            ValidateBindings(body, colliders, out _);
            foreach (Collider collider in colliders)
            {
                if (!collider.isTrigger && collider.gameObject.layer != layer)
                    collider.gameObject.layer = layer;
            }

            Validate(body, colliders, layerName);
        }

        public static void Validate(Rigidbody body, Collider[] colliders,
            string layerName)
        {
            int layer = RequireProfileLayer(layerName);
            ValidateGhostLayerMatrix();
            ValidateBindings(body, colliders, out int solidColliderCount);
            foreach (Collider collider in colliders)
            {
                if (!collider.isTrigger && collider.gameObject.layer != layer)
                {
                    throw new InvalidOperationException(
                        $"Collider '{collider.name}' must use the '{layerName}' " +
                        "collision profile.");
                }
            }

            if (solidColliderCount == 0)
            {
                throw new InvalidOperationException(
                    $"Rigidbody '{body.name}' requires at least one enabled solid " +
                    "collider for its collision profile.");
            }
        }

        public static bool BelongsToGhostMover(Collider collider)
        {
            if (collider == null)
                throw new ArgumentNullException(nameof(collider));

            int ghostLayer = RequireLayer(GhostMover);
            if (!collider.isTrigger)
                return collider.gameObject.layer == ghostLayer;

            Rigidbody body = collider.attachedRigidbody;
            if (body == null)
                return false;
            foreach (Collider sibling in
                     body.GetComponentsInChildren<Collider>(includeInactive: true))
            {
                if (sibling != null && !sibling.isTrigger &&
                    sibling.gameObject.layer == ghostLayer)
                {
                    return true;
                }
            }

            return false;
        }

        public static void ValidateGhostLayerMatrix()
        {
            if (Application.isPlaying && _runtimeMatrixValidated)
                return;

            int ghostLayer = RequireLayer(GhostMover);
            for (int layer = 0; layer < PhysicsLayerCount; layer++)
            {
                if (!UnityEngine.Physics.GetIgnoreLayerCollision(
                        ghostLayer, layer))
                {
                    throw new InvalidOperationException(
                        $"Physics layer '{GhostMover}' must ignore layer {layer}. " +
                        "Configure all 32 collision-matrix entries before entering play mode.");
                }
            }

            if (Application.isPlaying)
                _runtimeMatrixValidated = true;
        }

        private static int RequireProfileLayer(string layerName)
        {
            if (layerName != GhostMover && layerName != TrafficObstacle)
            {
                throw new ArgumentException(
                    $"Unsupported mover collision profile '{layerName}'.",
                    nameof(layerName));
            }

            return RequireLayer(layerName);
        }

        private static int RequireLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                throw new InvalidOperationException(
                    $"Required physics layer '{layerName}' is missing.");
            }

            return layer;
        }

        private static void ValidateBindings(Rigidbody body,
            Collider[] colliders, out int solidColliderCount)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));
            if (colliders == null || colliders.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Rigidbody '{body.name}' requires registered colliders.");
            }
            if (!body.gameObject.activeInHierarchy || !body.detectCollisions)
            {
                throw new InvalidOperationException(
                    $"Rigidbody '{body.name}' must be active and queryable.");
            }

            solidColliderCount = 0;
            foreach (Collider collider in colliders)
            {
                if (collider == null)
                {
                    throw new InvalidOperationException(
                        $"Rigidbody '{body.name}' has a missing registered collider.");
                }
                if (collider.attachedRigidbody != body)
                {
                    throw new InvalidOperationException(
                        $"Collider '{collider.name}' is not attached to Rigidbody " +
                        $"'{body.name}'.");
                }
                if (collider.isTrigger)
                    continue;
                if (!collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    throw new InvalidOperationException(
                        $"Solid collider '{collider.name}' must remain enabled and " +
                        "queryable.");
                }

                solidColliderCount++;
            }
        }
    }
}
