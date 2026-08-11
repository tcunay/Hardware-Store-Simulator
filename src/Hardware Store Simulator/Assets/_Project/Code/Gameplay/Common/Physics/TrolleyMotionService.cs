using System;
using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public sealed class TrolleyMotionService : ITrolleyMotionService
    {
        private const int MaxQueryHits = 64;
        private const float PoseTolerance = 0.001f;
        private const float MinimumSweepDistance = 0.0001f;
        private const float MaximumRotationStep = 5f;
        private const float MinimumQuaternionLength = 0.999f;
        private const float MaximumQuaternionLength = 1.001f;
        private const float MinimumUprightDot = 0.999f;

        private readonly RaycastHit[] _sweepHits = new RaycastHit[MaxQueryHits];
        private readonly Collider[] _overlapHits = new Collider[MaxQueryHits];

        public bool CanMoveTo(Rigidbody trolleyBody, Collider[] trolleyColliders,
            CharacterController sourceController, Vector3 targetPosition,
            Quaternion targetRotation)
        {
            BoxCollider hull = ValidateAndGetHull(
                trolleyBody, trolleyColliders, sourceController);
            ValidatePose(targetPosition, targetRotation, "target trolley");

            Transform trolleyTransform = trolleyBody.transform;
            ValidatePose(trolleyTransform.position, trolleyTransform.rotation,
                "current trolley");
            if ((trolleyBody.position - trolleyTransform.position).sqrMagnitude >
                PoseTolerance * PoseTolerance ||
                Quaternion.Angle(trolleyBody.rotation, trolleyTransform.rotation) >
                PoseTolerance)
            {
                throw new InvalidOperationException(
                    "Trolley Rigidbody and Transform poses are out of sync.");
            }

            Vector3 currentHullCenter = hull.transform.TransformPoint(hull.center);
            Vector3 hullCenterInTrolleySpace =
                trolleyTransform.InverseTransformPoint(currentHullCenter);
            Vector3 targetHullCenter =
                targetPosition + targetRotation * hullCenterInTrolleySpace;
            Quaternion hullRotationInTrolleySpace =
                Quaternion.Inverse(trolleyTransform.rotation) * hull.transform.rotation;
            Quaternion currentHullRotation = hull.transform.rotation;
            Quaternion targetHullRotation = targetRotation * hullRotationInTrolleySpace;
            Vector3 halfExtents = Vector3.Scale(
                hull.size * 0.5f, PositiveScale(hull.transform.lossyScale));

            ValidateVector(currentHullCenter, "current trolley hull center");
            ValidateVector(targetHullCenter, "target trolley hull center");
            ValidateVector(halfExtents, "trolley hull half extents");
            ValidateQuaternion(currentHullRotation, "current trolley hull rotation");
            ValidateQuaternion(targetHullRotation, "target trolley hull rotation");

            Vector3 displacement = targetHullCenter - currentHullCenter;
            float distance = displacement.magnitude;
            if (distance > MinimumSweepDistance &&
                HasBlockingSweep(
                    currentHullCenter,
                    halfExtents,
                    displacement / distance,
                    currentHullRotation,
                    distance,
                    trolleyTransform,
                    sourceController))
            {
                return false;
            }

            if (HasBlockingRotationPath(
                    currentHullCenter,
                    targetHullCenter,
                    halfExtents,
                    currentHullRotation,
                    targetHullRotation,
                    trolleyTransform,
                    sourceController))
            {
                return false;
            }

            return !HasBlockingOverlap(
                targetHullCenter,
                halfExtents,
                targetHullRotation,
                trolleyTransform,
                sourceController,
                "target overlap");
        }

        private bool HasBlockingSweep(Vector3 origin, Vector3 halfExtents,
            Vector3 direction, Quaternion orientation, float distance,
            Transform trolleyTransform, CharacterController sourceController)
        {
            int hitCount = UnityEngine.Physics.BoxCastNonAlloc(
                origin,
                halfExtents,
                direction,
                _sweepHits,
                orientation,
                distance,
                UnityEngine.Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            EnsureBufferWasNotSaturated(hitCount, _sweepHits.Length, "trolley box cast");

            for (int index = 0; index < hitCount; index++)
            {
                Collider hit = _sweepHits[index].collider;
                if (hit == null)
                    throw new InvalidOperationException("Trolley box cast returned a missing collider.");
                if (!ShouldIgnore(hit, trolleyTransform, sourceController))
                    return true;
            }

            return false;
        }

        private bool HasBlockingRotationPath(Vector3 currentCenter,
            Vector3 targetCenter, Vector3 halfExtents, Quaternion currentRotation,
            Quaternion targetRotation, Transform trolleyTransform,
            CharacterController sourceController)
        {
            float rotationAngle = Quaternion.Angle(currentRotation, targetRotation);
            int stepCount = Mathf.CeilToInt(rotationAngle / MaximumRotationStep);
            for (int step = 1; step < stepCount; step++)
            {
                float progress = (float)step / stepCount;
                if (HasBlockingOverlap(
                        Vector3.Lerp(currentCenter, targetCenter, progress),
                        halfExtents,
                        Quaternion.Slerp(currentRotation, targetRotation, progress),
                        trolleyTransform,
                        sourceController,
                        "rotation-path overlap"))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasBlockingOverlap(Vector3 center, Vector3 halfExtents,
            Quaternion orientation, Transform trolleyTransform,
            CharacterController sourceController, string operation)
        {
            int hitCount = UnityEngine.Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                _overlapHits,
                orientation,
                UnityEngine.Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            EnsureBufferWasNotSaturated(hitCount, _overlapHits.Length, operation);

            for (int index = 0; index < hitCount; index++)
            {
                Collider hit = _overlapHits[index];
                if (hit == null)
                    throw new InvalidOperationException(
                        $"Trolley {operation} returned a missing collider.");
                if (!ShouldIgnore(hit, trolleyTransform, sourceController))
                    return true;
            }

            return false;
        }

        private static BoxCollider ValidateAndGetHull(Rigidbody trolleyBody,
            Collider[] trolleyColliders, CharacterController sourceController)
        {
            if (trolleyBody == null)
                throw new ArgumentNullException(nameof(trolleyBody));
            if (trolleyColliders == null)
                throw new ArgumentNullException(nameof(trolleyColliders));
            if (sourceController == null)
                throw new ArgumentNullException(nameof(sourceController));
            if (!trolleyBody.gameObject.activeInHierarchy || !trolleyBody.isKinematic ||
                trolleyBody.useGravity || !trolleyBody.detectCollisions)
            {
                throw new InvalidOperationException(
                    "Trolley motion requires an active collision-enabled kinematic Rigidbody.");
            }
            if (!sourceController.enabled || !sourceController.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException(
                    "Trolley motion requires an active source CharacterController.");
            }

            Vector3 rootScale = trolleyBody.transform.lossyScale;
            ValidateVector(rootScale, "trolley root scale");
            if (!ApproximatelyOne(rootScale.x) || !ApproximatelyOne(rootScale.y) ||
                !ApproximatelyOne(rootScale.z))
            {
                throw new InvalidOperationException(
                    "Trolley motion requires a unit world scale on the Rigidbody root.");
            }

            BoxCollider hull = null;
            int enabledSolidColliderCount = 0;
            foreach (Collider trolleyCollider in trolleyColliders)
            {
                if (trolleyCollider == null)
                    throw new InvalidOperationException(
                        "Trolley Colliders contains a missing reference.");
                if (trolleyCollider.attachedRigidbody != trolleyBody ||
                    !IsInHierarchy(trolleyCollider.transform, trolleyBody.transform))
                {
                    throw new InvalidOperationException(
                        $"Trolley collider {trolleyCollider.name} is not attached to its Rigidbody.");
                }
                if (!trolleyCollider.enabled || trolleyCollider.isTrigger)
                    continue;

                enabledSolidColliderCount++;
                if (trolleyCollider is not BoxCollider boxCollider)
                {
                    throw new InvalidOperationException(
                        "The enabled trolley collision hull must be a BoxCollider.");
                }

                hull = boxCollider;
            }

            if (enabledSolidColliderCount != 1 || hull == null)
            {
                throw new InvalidOperationException(
                    "Trolley motion requires exactly one enabled non-trigger BoxCollider hull.");
            }

            ValidateVector(hull.center, "trolley hull center");
            ValidateVector(hull.size, "trolley hull size");
            Vector3 hullScale = PositiveScale(hull.transform.lossyScale);
            if (hull.size.x <= 0f || hull.size.y <= 0f || hull.size.z <= 0f ||
                hullScale.x <= 0f || hullScale.y <= 0f || hullScale.z <= 0f)
            {
                throw new InvalidOperationException(
                    "Trolley collision hull must have positive dimensions and scale.");
            }

            return hull;
        }

        private static bool ShouldIgnore(Collider candidate, Transform trolleyTransform,
            CharacterController sourceController) =>
            IsInHierarchy(candidate.transform, trolleyTransform) ||
            candidate == sourceController;

        private static bool IsInHierarchy(Transform candidate, Transform root) =>
            candidate == root || candidate.IsChildOf(root);

        private static void EnsureBufferWasNotSaturated(int hitCount,
            int capacity, string operation)
        {
            if (hitCount >= capacity)
            {
                throw new InvalidOperationException(
                    $"Physics {operation} saturated its {capacity}-hit buffer.");
            }
        }

        private static Vector3 PositiveScale(Vector3 scale)
        {
            ValidateVector(scale, "collider scale");
            if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f)
            {
                throw new InvalidOperationException(
                    "Trolley collision queries require a positive collider scale.");
            }

            return scale;
        }

        private static void ValidatePose(Vector3 position, Quaternion rotation,
            string owner)
        {
            ValidateVector(position, $"{owner} position");
            ValidateQuaternion(rotation, $"{owner} rotation");
            if (Vector3.Dot(rotation * Vector3.up, Vector3.up) < MinimumUprightDot)
            {
                throw new InvalidOperationException(
                    $"The {owner} rotation must keep the trolley upright.");
            }
        }

        private static void ValidateVector(Vector3 value, string owner)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z))
                throw new InvalidOperationException($"The {owner} must be finite.");
        }

        private static void ValidateQuaternion(Quaternion value, string owner)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) ||
                !IsFinite(value.z) || !IsFinite(value.w))
            {
                throw new InvalidOperationException($"The {owner} must be finite.");
            }

            float length = Mathf.Sqrt(
                value.x * value.x + value.y * value.y +
                value.z * value.z + value.w * value.w);
            if (length < MinimumQuaternionLength || length > MaximumQuaternionLength)
            {
                throw new InvalidOperationException(
                    $"The {owner} must be a normalized quaternion.");
            }
        }

        private static bool ApproximatelyOne(float value) =>
            Mathf.Abs(value - 1f) <= PoseTolerance;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
