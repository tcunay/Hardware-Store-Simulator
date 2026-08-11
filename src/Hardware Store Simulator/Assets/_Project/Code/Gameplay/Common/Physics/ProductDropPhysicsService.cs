using System;
using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public sealed class ProductDropPhysicsService : IProductDropPhysicsService
    {
        private const int MaxBlockingHits = 32;
        private const float MinimumDirectionSqrMagnitude = 0.000001f;

        private readonly RaycastHit[] _sweepHits = new RaycastHit[MaxBlockingHits];
        private readonly Collider[] _overlapHits = new Collider[MaxBlockingHits];

        public bool TryGetSafeDropPosition(
            Vector3 origin,
            Vector3 forward,
            float forwardDistance,
            float collisionRadius,
            CharacterController sourceController,
            out Vector3 position)
        {
            ValidateArguments(
                origin,
                forward,
                forwardDistance,
                collisionRadius,
                sourceController);
            Vector3 direction = forward.normalized;
            position = origin + direction * forwardDistance;

            if (HasBlockingOverlap(
                    origin,
                    collisionRadius,
                    sourceController,
                    "product drop origin overlap"))
            {
                return false;
            }

            int sweepHitCount = UnityEngine.Physics.SphereCastNonAlloc(
                origin,
                collisionRadius,
                direction,
                _sweepHits,
                forwardDistance,
                UnityEngine.Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            ThrowIfSaturated(sweepHitCount, _sweepHits.Length, "product drop sweep");

            for (int index = 0; index < sweepHitCount; index++)
            {
                Collider hitCollider = _sweepHits[index].collider;
                if (hitCollider == null)
                    throw new InvalidOperationException(
                        "Product drop sweep returned a missing collider.");
                if (hitCollider != sourceController)
                    return false;
            }

            return !HasBlockingOverlap(
                position,
                collisionRadius,
                sourceController,
                "product drop target overlap");
        }

        private bool HasBlockingOverlap(
            Vector3 position,
            float collisionRadius,
            CharacterController sourceController,
            string query)
        {
            int overlapHitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                position,
                collisionRadius,
                _overlapHits,
                UnityEngine.Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            ThrowIfSaturated(overlapHitCount, _overlapHits.Length, query);

            for (int index = 0; index < overlapHitCount; index++)
            {
                if (_overlapHits[index] == null)
                    throw new InvalidOperationException(
                        $"The {query} returned a missing collider.");
                if (_overlapHits[index] != sourceController)
                    return true;
            }

            return false;
        }

        private static void ValidateArguments(
            Vector3 origin,
            Vector3 forward,
            float forwardDistance,
            float collisionRadius,
            CharacterController sourceController)
        {
            if (sourceController == null)
                throw new ArgumentNullException(nameof(sourceController));
            if (!sourceController.enabled ||
                !sourceController.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException(
                    "Product drop requires an active source CharacterController.");
            }

            RequireFinite(origin, nameof(origin));
            RequireFinite(forward, nameof(forward));
            RequirePositiveFinite(forwardDistance, nameof(forwardDistance));
            RequirePositiveFinite(collisionRadius, nameof(collisionRadius));
            if (forward.sqrMagnitude <= MinimumDirectionSqrMagnitude)
                throw new ArgumentOutOfRangeException(
                    nameof(forward),
                    "Product drop forward direction must be non-zero.");
            if (collisionRadius > forwardDistance)
                throw new ArgumentOutOfRangeException(
                    nameof(collisionRadius),
                    "Product drop collision radius must not exceed forward distance.");
        }

        private static void RequireFinite(Vector3 value, string argument)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z))
                throw new ArgumentOutOfRangeException(
                    argument,
                    "Product drop vectors must contain only finite values.");
        }

        private static void RequirePositiveFinite(float value, string argument)
        {
            if (!IsFinite(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(
                    argument,
                    "Product drop distances and radii must be finite and greater than zero.");
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ThrowIfSaturated(int hitCount, int capacity, string query)
        {
            if (hitCount == capacity)
            {
                throw new InvalidOperationException(
                    $"The fixed {query} buffer is saturated at {capacity} hits. " +
                    "Increase its capacity before relying on the query result.");
            }
        }
    }
}
