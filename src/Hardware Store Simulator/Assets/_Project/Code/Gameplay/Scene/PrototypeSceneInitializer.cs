using System;
using HardwareStore.Gameplay.Presentation;
using UnityEngine;
using Zenject;

namespace HardwareStore.Gameplay.Scene
{
    public sealed class PrototypeSceneInitializer : MonoBehaviour, IInitializable, IDisposable
    {
        [SerializeField] private SpawnPointMarker[] _spawnPoints;
        [SerializeField] private SceneRouteMarker[] _routes;
        [SerializeField] private CustomerFlowLayoutMarker _customerFlowLayout;
        [SerializeField] private SceneViewMarker[] _sceneViews;
        [SerializeField] private PrototypeHudView _hudView;
        [SerializeField] private PrototypeAudioView _audioView;
        [SerializeField] private PrototypeDayNightView _dayNightView;

        private IStoreSceneData _sceneData;

        [Inject]
        private void Construct(IStoreSceneData sceneData) =>
            _sceneData = sceneData;

        public void Configure(SpawnPointMarker[] spawnPoints, SceneRouteMarker[] routes,
            CustomerFlowLayoutMarker customerFlowLayout,
            SceneViewMarker[] sceneViews, PrototypeHudView hudView,
            PrototypeAudioView audioView, PrototypeDayNightView dayNightView)
        {
            _spawnPoints = spawnPoints != null
                ? spawnPoints
                : throw new ArgumentNullException(nameof(spawnPoints));
            _routes = routes != null
                ? routes
                : throw new ArgumentNullException(nameof(routes));
            _customerFlowLayout = customerFlowLayout != null
                ? customerFlowLayout
                : throw new ArgumentNullException(nameof(customerFlowLayout));
            _sceneViews = sceneViews != null
                ? sceneViews
                : throw new ArgumentNullException(nameof(sceneViews));
            _hudView = hudView != null ? hudView : throw new ArgumentNullException(nameof(hudView));
            _audioView = audioView != null ? audioView : throw new ArgumentNullException(nameof(audioView));
            _dayNightView = dayNightView != null
                ? dayNightView
                : throw new ArgumentNullException(nameof(dayNightView));
        }

        public void Initialize() =>
            _sceneData.Register(
                _spawnPoints,
                _routes,
                _customerFlowLayout,
                _sceneViews,
                _hudView,
                _audioView,
                _dayNightView);

        public void Dispose() =>
            _sceneData.Unregister();
    }
}
