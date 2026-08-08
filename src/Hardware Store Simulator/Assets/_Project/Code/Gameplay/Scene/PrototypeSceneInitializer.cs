using System;
using HardwareStore.Gameplay.Presentation;
using HardwareStore.Gameplay.Views;
using UnityEngine;
using Zenject;

namespace HardwareStore.Gameplay.Scene
{
    public sealed class PrototypeSceneInitializer : MonoBehaviour, IInitializable, IDisposable
    {
        [SerializeField] private SpawnPointMarker[] _spawnPoints;
        [SerializeField] private InteractionView _orderCounterView;
        [SerializeField] private LoadingZoneView _loadingZoneView;
        [SerializeField] private ProductView[] _productViews;
        [SerializeField] private PrototypeHudView _hudView;
        [SerializeField] private PrototypeAudioView _audioView;

        private IStoreSceneData _sceneData;

        [Inject]
        private void Construct(IStoreSceneData sceneData) =>
            _sceneData = sceneData;

        public void Configure(SpawnPointMarker[] spawnPoints, InteractionView orderCounterView,
            LoadingZoneView loadingZoneView, ProductView[] productViews, PrototypeHudView hudView,
            PrototypeAudioView audioView)
        {
            _spawnPoints = spawnPoints != null
                ? spawnPoints
                : throw new ArgumentNullException(nameof(spawnPoints));
            _orderCounterView = orderCounterView != null
                ? orderCounterView
                : throw new ArgumentNullException(nameof(orderCounterView));
            _loadingZoneView = loadingZoneView != null
                ? loadingZoneView
                : throw new ArgumentNullException(nameof(loadingZoneView));
            _productViews = productViews != null
                ? productViews
                : throw new ArgumentNullException(nameof(productViews));
            _hudView = hudView != null ? hudView : throw new ArgumentNullException(nameof(hudView));
            _audioView = audioView != null ? audioView : throw new ArgumentNullException(nameof(audioView));
        }

        public void Initialize() =>
            _sceneData.Register(
                _spawnPoints,
                _orderCounterView,
                _loadingZoneView,
                _productViews,
                _hudView,
                _audioView);

        public void Dispose() =>
            _sceneData.Unregister();
    }
}
