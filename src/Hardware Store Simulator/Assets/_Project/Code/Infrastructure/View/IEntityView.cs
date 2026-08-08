using UnityEngine;

namespace HardwareStore.Infrastructure.View
{
    public interface IEntityView
    {
        GameEntity Entity { get; }
        GameObject gameObject { get; }

        void SetEntity(GameEntity entity);
        void ReleaseEntity();
    }
}
