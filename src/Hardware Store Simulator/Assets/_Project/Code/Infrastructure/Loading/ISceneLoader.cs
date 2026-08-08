using System;

namespace HardwareStore.Infrastructure.Loading
{
    public interface ISceneLoader
    {
        void LoadScene(string sceneName, Action onLoaded = null);
    }
}
