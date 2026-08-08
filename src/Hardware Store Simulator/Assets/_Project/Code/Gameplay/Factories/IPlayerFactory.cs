using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public interface IPlayerFactory
    {
        GameEntity Create(Pose at);
    }
}
