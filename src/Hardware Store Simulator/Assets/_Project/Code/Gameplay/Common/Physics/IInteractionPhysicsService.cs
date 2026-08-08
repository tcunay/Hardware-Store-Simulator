using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public interface IInteractionPhysicsService
    {
        bool TryGetFocusedEntity(Camera viewCamera, float interactionDistance, float aimAssistRadius,
            out int entityId);
    }
}
