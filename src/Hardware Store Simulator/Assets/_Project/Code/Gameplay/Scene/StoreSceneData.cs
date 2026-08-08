using System;
using System.Collections.Generic;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Presentation;
using HardwareStore.Gameplay.Views;
using UnityEngine;

namespace HardwareStore.Gameplay.Scene
{
    public sealed class StoreSceneData : IStoreSceneData
    {
        private Dictionary<SpawnPointId, Pose> _spawnPoints;
        private InteractionView _orderCounterView;
        private LoadingZoneView _loadingZoneView;
        private IReadOnlyList<ProductView> _productViews;
        private IHudService _hud;
        private INotificationService _notifications;
        private IAudioService _audio;

        public bool IsRegistered { get; private set; }
        public InteractionView OrderCounterView => GetRegistered(_orderCounterView);
        public LoadingZoneView LoadingZoneView => GetRegistered(_loadingZoneView);
        public IReadOnlyList<ProductView> ProductViews => GetRegistered(_productViews);

        public Pose GetSpawnPoint(SpawnPointId id)
        {
            EnsureRegistered();

            if (!_spawnPoints.TryGetValue(id, out Pose pose))
                throw new KeyNotFoundException($"Spawn point '{id}' is not registered.");

            return pose;
        }

        public void Register(SpawnPointMarker[] spawnPoints, InteractionView orderCounterView,
            LoadingZoneView loadingZoneView, ProductView[] productViews,
            PrototypeHudView hudView, PrototypeAudioView audioView)
        {
            if (IsRegistered)
                throw new InvalidOperationException("Store scene data is already registered.");

            if (spawnPoints == null)
                throw new ArgumentNullException(nameof(spawnPoints));
            if (orderCounterView == null)
                throw new ArgumentNullException(nameof(orderCounterView));
            if (loadingZoneView == null)
                throw new ArgumentNullException(nameof(loadingZoneView));
            if (productViews == null)
                throw new ArgumentNullException(nameof(productViews));
            if (hudView == null)
                throw new ArgumentNullException(nameof(hudView));
            if (audioView == null)
                throw new ArgumentNullException(nameof(audioView));

            var spawnPointPoses = new Dictionary<SpawnPointId, Pose>(spawnPoints.Length);
            for (int index = 0; index < spawnPoints.Length; index++)
            {
                SpawnPointMarker spawnPoint = spawnPoints[index];
                if (spawnPoint == null)
                    throw new ArgumentException($"Spawn point at index {index} is missing.", nameof(spawnPoints));
                if (!spawnPointPoses.TryAdd(spawnPoint.Id, spawnPoint.Pose))
                    throw new ArgumentException(
                        $"Spawn point '{spawnPoint.Id}' is registered more than once.", nameof(spawnPoints));
            }

            ProductView[] products = (ProductView[])productViews.Clone();
            for (int index = 0; index < products.Length; index++)
            {
                if (products[index] == null)
                    throw new ArgumentException($"Product view at index {index} is missing.", nameof(productViews));
            }

            _spawnPoints = spawnPointPoses;
            _orderCounterView = orderCounterView;
            _loadingZoneView = loadingZoneView;
            _productViews = Array.AsReadOnly(products);
            _hud = hudView;
            _notifications = hudView;
            _audio = audioView;
            IsRegistered = true;
        }

        public void Unregister()
        {
            EnsureRegistered();

            IsRegistered = false;
            _spawnPoints.Clear();
            _spawnPoints = null;
            _orderCounterView = null;
            _loadingZoneView = null;
            _productViews = null;
            _hud = null;
            _notifications = null;
            _audio = null;
        }

        public void Play(AudioCueId cue)
        {
            EnsureRegistered();
            _audio.Play(cue);
        }

        public void Show(string message)
        {
            EnsureRegistered();
            _notifications.Show(message);
        }

        public void Present(HudSnapshot snapshot)
        {
            EnsureRegistered();
            _hud.Present(snapshot);
        }

        private T GetRegistered<T>(T value) where T : class
        {
            EnsureRegistered();
            return value;
        }

        private void EnsureRegistered()
        {
            if (!IsRegistered)
                throw new InvalidOperationException("Store scene data is not registered.");
        }
    }
}
