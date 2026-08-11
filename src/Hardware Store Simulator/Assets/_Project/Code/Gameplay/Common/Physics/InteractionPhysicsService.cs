using System;
using HardwareStore.Gameplay.Common.Collisions;
using HardwareStore.Gameplay.Views;
using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public sealed class InteractionPhysicsService : IInteractionPhysicsService
    {
        private const int MaxPhysicsHits = 128;
        private const float AimAngleWeight = 0.025f;
        private const float OcclusionTolerance = 0.001f;

        private readonly ICollisionRegistry _collisions;
        private readonly RaycastHit[] _castHits = new RaycastHit[MaxPhysicsHits];
        private readonly RaycastHit[] _visibilityHits = new RaycastHit[MaxPhysicsHits];

        public InteractionPhysicsService(ICollisionRegistry collisions) => _collisions = collisions;

        public int GetFocusCandidates(Camera viewCamera, float interactionDistance, float aimAssistRadius,
            InteractionFocusCandidate[] candidates)
        {
            if (viewCamera == null)
                throw new ArgumentNullException(nameof(viewCamera));
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));
            if (candidates.Length == 0)
                throw new ArgumentException("Focus candidate buffer must not be empty.", nameof(candidates));

            Ray ray = new(viewCamera.transform.position, viewCamera.transform.forward);
            int candidateCount = 0;

            int directHitCount = UnityEngine.Physics.RaycastNonAlloc(ray, _castHits, interactionDistance,
                UnityEngine.Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            EnsureQueryDidNotSaturate(directHitCount, _castHits, "interaction raycast");

            float nearestBlockerDistance = float.PositiveInfinity;
            for (int i = 0; i < directHitCount; i++)
            {
                RaycastHit hit = _castHits[i];
                if (BlocksDirectFocus(hit.collider) && hit.distance < nearestBlockerDistance)
                    nearestBlockerDistance = hit.distance;
            }

            for (int i = 0; i < directHitCount; i++)
            {
                RaycastHit hit = _castHits[i];
                if (hit.distance > nearestBlockerDistance + OcclusionTolerance ||
                    !TryResolveEntityId(hit.collider, out int entityId))
                {
                    continue;
                }

                candidateCount = AddOrImproveCandidate(candidates, candidateCount,
                    new InteractionFocusCandidate(entityId, hit.distance, isDirect: true));
            }

            int assistedHitCount = UnityEngine.Physics.SphereCastNonAlloc(ray, aimAssistRadius, _castHits,
                interactionDistance, UnityEngine.Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            EnsureQueryDidNotSaturate(assistedHitCount, _castHits, "interaction sphere cast");

            for (int i = 0; i < assistedHitCount; i++)
            {
                RaycastHit hit = _castHits[i];
                if (!TryResolveEntityId(hit.collider, out int entityId) ||
                    !HasLineOfSight(ray.origin, hit, entityId))
                {
                    continue;
                }

                Vector3 toHit = hit.collider.bounds.center - ray.origin;
                float angle = toHit.sqrMagnitude > Mathf.Epsilon
                    ? Vector3.Angle(ray.direction, toHit.normalized)
                    : 0f;
                float score = hit.distance + angle * AimAngleWeight;
                candidateCount = AddOrImproveCandidate(candidates, candidateCount,
                    new InteractionFocusCandidate(entityId, score, isDirect: false));
            }

            return candidateCount;
        }

        private bool HasLineOfSight(Vector3 origin, RaycastHit candidateHit, int candidateEntityId)
        {
            Vector3 targetPoint = ResolveLineOfSightTargetPoint(origin, candidateHit);
            Vector3 toTarget = targetPoint - origin;
            float targetDistance = toTarget.magnitude;
            if (targetDistance <= OcclusionTolerance)
                return true;

            Ray visibilityRay = new(origin, toTarget / targetDistance);
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(visibilityRay, _visibilityHits,
                targetDistance, UnityEngine.Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            EnsureQueryDidNotSaturate(hitCount, _visibilityHits, "interaction visibility raycast");

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _visibilityHits[i];
                if (hit.distance < targetDistance - OcclusionTolerance &&
                    BlocksAssistedFocus(hit.collider, candidateEntityId))
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector3 ResolveLineOfSightTargetPoint(Vector3 origin,
            RaycastHit candidateHit)
        {
            Vector3 targetPoint = candidateHit.point;
            if (candidateHit.distance <= OcclusionTolerance || targetPoint == Vector3.zero)
                targetPoint = candidateHit.collider.ClosestPoint(origin);
            if ((targetPoint - origin).sqrMagnitude <= Mathf.Epsilon)
                targetPoint = candidateHit.collider.bounds.center;

            return targetPoint;
        }

        private static bool BlocksDirectFocus(Collider hitCollider) =>
            !IsNonOccludingInteractionProxy(hitCollider);

        private bool BlocksAssistedFocus(Collider hitCollider, int candidateEntityId)
        {
            if (IsNonOccludingInteractionProxy(hitCollider))
                return false;

            return !TryResolveEntityId(hitCollider, out int hitEntityId) ||
                   hitEntityId != candidateEntityId;
        }

        private static bool IsNonOccludingInteractionProxy(Collider hitCollider) =>
            hitCollider.isTrigger &&
            hitCollider.GetComponentInParent<NonOccludingInteractionProxy>() != null;

        private static int AddOrImproveCandidate(InteractionFocusCandidate[] candidates,
            int candidateCount, InteractionFocusCandidate candidate)
        {
            for (int i = 0; i < candidateCount; i++)
            {
                InteractionFocusCandidate current = candidates[i];
                if (current.EntityId != candidate.EntityId)
                    continue;

                if (IsBetterPhysicalCandidate(candidate, current))
                    candidates[i] = candidate;

                return candidateCount;
            }

            if (candidateCount == candidates.Length)
            {
                throw new InvalidOperationException(
                    $"Interaction focus candidate buffer saturated at {candidates.Length} entries.");
            }

            candidates[candidateCount] = candidate;
            return candidateCount + 1;
        }

        private static bool IsBetterPhysicalCandidate(InteractionFocusCandidate candidate,
            InteractionFocusCandidate current)
        {
            if (candidate.IsDirect != current.IsDirect)
                return candidate.IsDirect;

            return candidate.Score < current.Score;
        }

        private static void EnsureQueryDidNotSaturate(int hitCount, RaycastHit[] hits, string queryName)
        {
            if (hitCount == hits.Length)
            {
                throw new InvalidOperationException(
                    $"The {queryName} buffer saturated at {hits.Length} hits.");
            }
        }

        private bool TryResolveEntityId(Collider hitCollider, out int entityId)
        {
            if (!_collisions.TryGet(hitCollider.GetEntityId(), out GameEntity entity) || !entity.isInteractable)
            {
                entityId = default;
                return false;
            }

            entityId = entity.EntityId;
            return true;
        }
    }
}
