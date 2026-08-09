using HardwareStore.Gameplay.Scene;

namespace HardwareStore.Gameplay.Factories
{
    public interface IStoreFactory
    {
        GameEntity Create(IStoreSceneData sceneData);
    }
}
