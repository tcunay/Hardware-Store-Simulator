using System;
using System.Collections.Generic;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.Presentation;
using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Scene
{
    public sealed class StoreSceneData : IStoreSceneData
    {
        private Dictionary<SpawnPointId, Pose> _spawnPoints;
        private Dictionary<SceneRouteId, Pose[]> _routes;
        private CustomerFlowSceneLayout _customerFlowLayout;
        private Dictionary<SceneViewId, EntityBehaviour> _sceneViews;
        private IHudService _hud;
        private INotificationService _notifications;
        private IAudioService _audio;
        private IDayNightPresentationService _dayNight;

        public bool IsRegistered { get; private set; }

        public Pose GetSpawnPoint(SpawnPointId id)
        {
            EnsureRegistered();

            if (!_spawnPoints.TryGetValue(id, out Pose pose))
                throw new KeyNotFoundException($"Spawn point '{id}' is not registered.");

            return pose;
        }

        public EntityBehaviour GetSceneView(SceneViewId id)
        {
            EnsureRegistered();

            if (!_sceneViews.TryGetValue(id, out EntityBehaviour view))
                throw new KeyNotFoundException($"Scene view '{id}' is not registered.");

            return view;
        }

        public CustomerFlowSceneLayout GetCustomerFlowLayout()
        {
            EnsureRegistered();
            return _customerFlowLayout.Clone();
        }

        public Pose[] GetRoute(SceneRouteId id)
        {
            EnsureRegistered();

            if (!_routes.TryGetValue(id, out Pose[] poses))
                throw new KeyNotFoundException($"Scene route '{id}' is not registered.");

            return (Pose[])poses.Clone();
        }

        public void Register(SpawnPointMarker[] spawnPoints, SceneRouteMarker[] routes,
            CustomerFlowLayoutMarker customerFlowLayout,
            SceneViewMarker[] sceneViews, PrototypeHudView hudView,
            PrototypeAudioView audioView, PrototypeDayNightView dayNightView)
        {
            if (IsRegistered)
                throw new InvalidOperationException("Store scene data is already registered.");

            if (spawnPoints == null)
                throw new ArgumentNullException(nameof(spawnPoints));
            if (routes == null)
                throw new ArgumentNullException(nameof(routes));
            if (customerFlowLayout == null)
                throw new ArgumentNullException(nameof(customerFlowLayout));
            if (sceneViews == null)
                throw new ArgumentNullException(nameof(sceneViews));
            if (hudView == null)
                throw new ArgumentNullException(nameof(hudView));
            if (audioView == null)
                throw new ArgumentNullException(nameof(audioView));
            if (dayNightView == null)
                throw new ArgumentNullException(nameof(dayNightView));

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

            var routesById = new Dictionary<SceneRouteId, Pose[]>(routes.Length);
            for (int index = 0; index < routes.Length; index++)
            {
                SceneRouteMarker marker = routes[index];
                if (marker == null)
                    throw new ArgumentException($"Scene route marker at index {index} is missing.", nameof(routes));
                if (!routesById.TryAdd(marker.Id, marker.Poses))
                    throw new ArgumentException(
                        $"Scene route '{marker.Id}' is registered more than once.", nameof(routes));
            }

            var viewsById = new Dictionary<SceneViewId, EntityBehaviour>(sceneViews.Length);
            for (int index = 0; index < sceneViews.Length; index++)
            {
                SceneViewMarker marker = sceneViews[index];
                if (marker == null)
                    throw new ArgumentException($"Scene view marker at index {index} is missing.", nameof(sceneViews));
                if (!viewsById.TryAdd(marker.Id, marker.View))
                    throw new ArgumentException(
                        $"Scene view '{marker.Id}' is registered more than once.", nameof(sceneViews));
            }

            _spawnPoints = spawnPointPoses;
            _routes = routesById;
            _customerFlowLayout = customerFlowLayout.Layout;
            _sceneViews = viewsById;
            _hud = hudView;
            _notifications = hudView;
            _audio = audioView;
            _dayNight = dayNightView;
            IsRegistered = true;
        }

        public void Unregister()
        {
            EnsureRegistered();

            IsRegistered = false;
            _spawnPoints.Clear();
            _spawnPoints = null;
            _routes.Clear();
            _routes = null;
            _customerFlowLayout = null;
            _sceneViews.Clear();
            _sceneViews = null;
            _hud = null;
            _notifications = null;
            _audio = null;
            _dayNight = null;
        }

        public void Play(AudioCueId cue)
        {
            EnsureRegistered();
            _audio.Play(cue);
        }

        public void Show(LocalizedText message)
        {
            EnsureRegistered();
            _notifications.Show(message);
        }

        public void Present(HudSnapshot snapshot)
        {
            EnsureRegistered();
            _hud.Present(snapshot);
        }

        public void Present(DayNightSnapshot snapshot)
        {
            EnsureRegistered();
            _dayNight.Present(snapshot);
        }

        public void PresentDayReport(DayReportSnapshot? snapshot)
        {
            EnsureRegistered();
            _hud.PresentDayReport(snapshot);
        }

        public void PresentConsultation(ConsultationSnapshot? snapshot)
        {
            EnsureRegistered();
            _hud.PresentConsultation(snapshot);
        }

        public void PresentProcurement(ProcurementSnapshot? snapshot)
        {
            EnsureRegistered();
            _hud.PresentProcurement(snapshot);
        }

        private void EnsureRegistered()
        {
            if (!IsRegistered)
                throw new InvalidOperationException("Store scene data is not registered.");
        }
    }
}
