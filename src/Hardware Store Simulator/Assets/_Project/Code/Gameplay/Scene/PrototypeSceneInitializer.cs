using System;
using HardwareStore.Gameplay.Presentation;
using UnityEngine;
using Zenject;

namespace HardwareStore.Gameplay.Scene
{
    public sealed class PrototypeSceneInitializer : MonoBehaviour, IInitializable, IDisposable
    {
        [SerializeField] private SpawnPointMarker[] _spawnPoints;
        [SerializeField] private SceneViewMarker[] _sceneViews;
        [SerializeField] private PrototypeHudView _hudView;
        [SerializeField] private PrototypeAudioView _audioView;

        private IStoreSceneData _sceneData;

        [Inject]
        private void Construct(IStoreSceneData sceneData) =>
            _sceneData = sceneData;

        public void Configure(SpawnPointMarker[] spawnPoints, SceneViewMarker[] sceneViews,
            PrototypeHudView hudView, PrototypeAudioView audioView)
        {
            _spawnPoints = spawnPoints != null
                ? spawnPoints
                : throw new ArgumentNullException(nameof(spawnPoints));
            _sceneViews = sceneViews != null
                ? sceneViews
                : throw new ArgumentNullException(nameof(sceneViews));
            _hudView = hudView != null ? hudView : throw new ArgumentNullException(nameof(hudView));
            _audioView = audioView != null ? audioView : throw new ArgumentNullException(nameof(audioView));
        }

        public void Initialize() =>
            _sceneData.Register(
                _spawnPoints,
                _sceneViews,
                _hudView,
                _audioView);

        public void Dispose() =>
            _sceneData.Unregister();
    }
}
