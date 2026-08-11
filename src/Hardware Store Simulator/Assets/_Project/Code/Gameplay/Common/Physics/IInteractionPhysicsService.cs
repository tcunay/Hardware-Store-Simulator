using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public readonly struct InteractionFocusCandidate
    {
        public InteractionFocusCandidate(int entityId, float score, bool isDirect)
        {
            EntityId = entityId;
            Score = score;
            IsDirect = isDirect;
        }

        public int EntityId { get; }
        public float Score { get; }
        public bool IsDirect { get; }
    }

    public interface IInteractionPhysicsService
    {
        int GetFocusCandidates(Camera viewCamera, float interactionDistance, float aimAssistRadius,
            InteractionFocusCandidate[] candidates);
    }
}
