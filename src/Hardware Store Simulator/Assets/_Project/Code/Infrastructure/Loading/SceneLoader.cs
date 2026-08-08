using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HardwareStore.Infrastructure.Loading
{
    public sealed class SceneLoader : ISceneLoader
    {
        private readonly ICoroutineRunner _coroutineRunner;

        public SceneLoader(ICoroutineRunner coroutineRunner) => _coroutineRunner = coroutineRunner;

        public void LoadScene(string sceneName, Action onLoaded = null)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                throw new ArgumentException("A scene name is required.", nameof(sceneName));

            _coroutineRunner.StartCoroutine(LoadSceneCoroutine(sceneName, onLoaded));
        }

        private static IEnumerator LoadSceneCoroutine(string sceneName, Action onLoaded)
        {
            if (SceneManager.GetActiveScene().name == sceneName)
            {
                yield return null;
                onLoaded?.Invoke();
                yield break;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
                throw new InvalidOperationException($"Unity could not start loading scene '{sceneName}'.");

            while (!operation.isDone)
                yield return null;

            onLoaded?.Invoke();
        }
    }
}
