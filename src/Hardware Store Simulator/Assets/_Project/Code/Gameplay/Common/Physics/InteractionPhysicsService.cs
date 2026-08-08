using HardwareStore.Gameplay.Common.Collisions;
using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public sealed class InteractionPhysicsService : IInteractionPhysicsService
    {
        private const int MaxAimAssistHits = 24;

        private readonly ICollisionRegistry _collisions;
        private readonly RaycastHit[] _aimAssistHits = new RaycastHit[MaxAimAssistHits];

        public InteractionPhysicsService(ICollisionRegistry collisions) => _collisions = collisions;

        public bool TryGetFocusedEntity(Camera viewCamera, float interactionDistance, float aimAssistRadius,
            out int entityId)
        {
            Ray ray = new(viewCamera.transform.position, viewCamera.transform.forward);
            float selectableDistance = interactionDistance;

            if (UnityEngine.Physics.Raycast(ray, out RaycastHit directHit, interactionDistance,
                    UnityEngine.Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                if (TryResolveEntityId(directHit.collider, out entityId))
                    return true;

                selectableDistance = Mathf.Min(interactionDistance, directHit.distance + aimAssistRadius);
            }

            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(ray, aimAssistRadius, _aimAssistHits,
                interactionDistance, UnityEngine.Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            float bestScore = float.PositiveInfinity;
            int bestEntityId = default;
            bool found = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _aimAssistHits[i];
                if (hit.distance > selectableDistance || !TryResolveEntityId(hit.collider, out int candidateEntityId))
                    continue;

                Vector3 toHit = hit.collider.bounds.center - ray.origin;
                float angle = Vector3.Angle(ray.direction, toHit.normalized);
                float score = hit.distance + angle * 0.025f;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                bestEntityId = candidateEntityId;
                found = true;
            }

            entityId = bestEntityId;
            return found;
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
