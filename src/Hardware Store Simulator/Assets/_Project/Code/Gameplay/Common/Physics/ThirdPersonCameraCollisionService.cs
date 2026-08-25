using System;
using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public sealed class ThirdPersonCameraCollisionService :
        IThirdPersonCameraCollisionService
    {
        private const int MaxQueryHits = 64;
        private const float MinimumDistance = 0.0001f;

        private readonly RaycastHit[] _hits = new RaycastHit[MaxQueryHits];

        public Vector3 ResolvePosition(
            Vector3 pivotPosition,
            Vector3 desiredPosition,
            float collisionRadius,
            float collisionPadding,
            Transform driverRoot,
            Transform vehicleRoot)
        {
            ValidateVector(pivotPosition, nameof(pivotPosition));
            ValidateVector(desiredPosition, nameof(desiredPosition));
            if (!IsFinite(collisionRadius) || collisionRadius <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(collisionRadius),
                    "Third-person camera collision radius must be finite and positive.");
            }
            if (!IsFinite(collisionPadding) || collisionPadding < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(collisionPadding),
                    "Third-person camera collision padding must be finite and non-negative.");
            }
            ValidateRoot(driverRoot, nameof(driverRoot));
            ValidateRoot(vehicleRoot, nameof(vehicleRoot));

            Vector3 displacement = desiredPosition - pivotPosition;
            float distance = displacement.magnitude;
            if (distance <= MinimumDistance)
                return pivotPosition;

            Vector3 direction = displacement / distance;
            UnityEngine.Physics.SyncTransforms();
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                pivotPosition,
                collisionRadius,
                direction,
                _hits,
                distance,
                UnityEngine.Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            if (hitCount >= _hits.Length)
            {
                throw new InvalidOperationException(
                    "Third-person camera collision buffer was saturated.");
            }

            float resolvedDistance = distance;
            for (int index = 0; index < hitCount; index++)
            {
                Collider hitCollider = _hits[index].collider;
                if (hitCollider == null)
                {
                    throw new InvalidOperationException(
                        "Third-person camera collision query returned a missing collider.");
                }
                if (hitCollider.transform.IsChildOf(driverRoot) ||
                    hitCollider.transform.IsChildOf(vehicleRoot))
                {
                    continue;
                }

                resolvedDistance = Mathf.Min(
                    resolvedDistance,
                    Mathf.Max(0f, _hits[index].distance - collisionPadding));
            }

            return pivotPosition + direction * resolvedDistance;
        }

        private static void ValidateRoot(Transform root, string parameterName)
        {
            if (root == null)
                throw new ArgumentNullException(parameterName);
            if (!root.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException(
                    $"Third-person camera requires active {parameterName}.");
            }
        }

        private static void ValidateVector(Vector3 value, string parameterName)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Third-person camera positions must be finite.");
            }
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
